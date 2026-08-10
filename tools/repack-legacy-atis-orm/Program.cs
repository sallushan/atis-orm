using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;
using Mono.Cecil;

namespace RepackLegacyAtisOrm
{
    /// <summary>
    /// Rebuilds <c>local-packages/Atis.ORM.Legacy.&lt;version&gt;.nupkg</c> from the untouched
    /// <c>local-packages/atis.orm.&lt;version&gt;.nupkg</c> that ships alongside it.
    ///
    /// <para>Why this exists: .NET assembly identity is compared case-insensitively, so the legacy
    /// engine's assembly (<c>Atis.ORM</c>) and this repo's (<c>Atis.Orm</c>) are the *same* assembly
    /// to the runtime. Referencing both yields MSB3243 ("No way to resolve conflict … Choosing …
    /// arbitrarily"), one of the two is dropped from <c>bin\</c>, and whichever lost fails at run
    /// time with a <see cref="TypeLoadException"/>. Renaming the legacy assembly is what lets the
    /// benchmark host load both engines in one process and report them in one table.</para>
    ///
    /// <para>What changes: the assembly name, the module name, and the strong-name signature (dropped,
    /// since the identity it covers no longer matches and .NET Core does not verify it anyway). No IL
    /// is touched, and the CLR namespaces stay <c>Atis.ORM.*</c> — so benchmark code against the
    /// repacked package is written exactly as it would be against the real one.</para>
    /// </summary>
    internal static class Program
    {
        private const string SourceAssemblyName = "Atis.ORM";
        private const string TargetAssemblyName = "Atis.ORM.Legacy";

        private static int Main(string[] args)
        {
            try
            {
                var repoRoot = args.Length > 0
                    ? Path.GetFullPath(args[0])
                    : FindRepoRoot();
                var feed = Path.Combine(repoRoot, "local-packages");

                var source = Directory.GetFiles(feed, "atis.orm.*.nupkg")
                                      .Where(f => !Path.GetFileName(f).StartsWith(TargetAssemblyName, StringComparison.OrdinalIgnoreCase))
                                      .OrderBy(f => f)
                                      .LastOrDefault()
                    ?? throw new FileNotFoundException($"No original atis.orm.*.nupkg found in '{feed}'.");

                Console.WriteLine($"source : {source}");
                var output = Repack(source, feed);
                Console.WriteLine($"output : {output}");
                Console.WriteLine("done.");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                return 1;
            }
        }

        /// <summary>
        /// Walks up from the tool's own location until the folder holding <c>local-packages</c> is found,
        /// so the tool works no matter which directory it is invoked from.
        /// </summary>
        private static string FindRepoRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "local-packages")))
                dir = dir.Parent;

            if (dir == null)
                throw new DirectoryNotFoundException("Could not locate the repo root (no ancestor contains 'local-packages'). Pass it as the first argument.");

            return dir.FullName;
        }

        private static string Repack(string sourceNupkg, string outputFolder)
        {
            var staging = Path.Combine(Path.GetTempPath(), "repack-legacy-atis-orm-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(staging);
                ZipFile.ExtractToDirectory(sourceNupkg, staging);

                // Drop the OPC plumbing: NuGet regenerates _rels/[Content_Types]/.psmdcp on pack, and
                // carrying the originals over would leave them describing the old package name.
                DeleteIfExists(Path.Combine(staging, "_rels"));
                DeleteIfExists(Path.Combine(staging, "package"));
                DeleteIfExists(Path.Combine(staging, "[Content_Types].xml"));

                var version = RewriteNuspec(staging);
                RewriteAssembly(staging);
                RewriteXmlDoc(staging);

                var output = Path.Combine(outputFolder, $"{TargetAssemblyName}.{version}.nupkg");
                if (File.Exists(output))
                    File.Delete(output);
                ZipFile.CreateFromDirectory(staging, output, CompressionLevel.Optimal, includeBaseDirectory: false);
                return output;
            }
            finally
            {
                DeleteIfExists(staging);
            }
        }

        /// <summary>
        /// Repoints the nuspec at the new package id and records where it came from. Dependencies are
        /// left alone: Atis.StringExtensions and System.Data.SqlClient are both on nuget.org.
        /// </summary>
        private static string RewriteNuspec(string staging)
        {
            var nuspecPath = Directory.GetFiles(staging, "*.nuspec").Single();
            var doc = XDocument.Load(nuspecPath);
            var ns = doc.Root.GetDefaultNamespace();
            var metadata = doc.Root.Element(ns + "metadata");

            var originalId = (string)metadata.Element(ns + "id");
            var version = (string)metadata.Element(ns + "version");

            metadata.Element(ns + "id").Value = TargetAssemblyName;

            var description = metadata.Element(ns + "description");
            description.Value =
                $"Repack of {originalId} {version} with the assembly renamed to '{TargetAssemblyName}' so it can be " +
                $"loaded alongside this repo's 'Atis.Orm' assembly, whose identity is otherwise identical " +
                $"(assembly names are compared case-insensitively). Benchmarks only — not for production use. " +
                $"Original description: {description.Value}";

            doc.Save(nuspecPath);
            File.Move(nuspecPath, Path.Combine(staging, TargetAssemblyName + ".nuspec"));
            return version;
        }

        private static void RewriteAssembly(string staging)
        {
            foreach (var dll in Directory.GetFiles(staging, SourceAssemblyName + ".dll", SearchOption.AllDirectories))
            {
                var renamed = Path.Combine(Path.GetDirectoryName(dll), TargetAssemblyName + ".dll");

                using (var assembly = AssemblyDefinition.ReadAssembly(dll))
                {
                    assembly.Name.Name = TargetAssemblyName;
                    assembly.MainModule.Name = TargetAssemblyName + ".dll";

                    // The original is strong-named. That signature was computed over the old identity and
                    // cannot be reproduced without the private key, so clear the public key and the signed
                    // flag rather than shipping a package that claims a signature it does not have.
                    assembly.Name.PublicKey = Array.Empty<byte>();
                    assembly.Name.HasPublicKey = false;
                    assembly.MainModule.Attributes &= ~ModuleAttributes.StrongNameSigned;

                    assembly.Write(renamed);
                }

                File.Delete(dll);
                Console.WriteLine($"renamed: {Relative(staging, dll)} -> {Relative(staging, renamed)}");
            }
        }

        /// <summary>
        /// The XML doc file is matched to its assembly by file name, and its <c>&lt;assembly&gt;&lt;name&gt;</c>
        /// element must agree, or IDEs silently stop showing IntelliSense for the renamed assembly.
        /// </summary>
        private static void RewriteXmlDoc(string staging)
        {
            foreach (var xml in Directory.GetFiles(staging, SourceAssemblyName + ".xml", SearchOption.AllDirectories))
            {
                var doc = XDocument.Load(xml);
                var name = doc.Root?.Element("assembly")?.Element("name");
                if (name != null)
                    name.Value = TargetAssemblyName;

                var renamed = Path.Combine(Path.GetDirectoryName(xml), TargetAssemblyName + ".xml");
                doc.Save(renamed);
                File.Delete(xml);
                Console.WriteLine($"renamed: {Relative(staging, xml)} -> {Relative(staging, renamed)}");
            }
        }

        private static string Relative(string root, string path) =>
            path.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar);

        private static void DeleteIfExists(string path)
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
            else if (File.Exists(path))
                File.Delete(path);
        }
    }
}
