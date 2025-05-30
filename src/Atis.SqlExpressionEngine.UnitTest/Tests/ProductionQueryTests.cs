﻿namespace Atis.SqlExpressionEngine.UnitTest.Tests
{
    [TestClass]
    public class ProductionQueryTests : TestBase
    {
        [TestMethod]
        public void Bench_stock_complex_query_test()
        {
            var benchStockRequisitionLines = new Queryable<BenchStock_RequisitionLine>(this.queryProvider);
            var q = this.queryProvider.From(() => new
            {
                br = QueryExtensions.Table<BenchStock_Requisition>(),
                tran_Site = QueryExtensions.Table<SiteBase>(),
                pid_CreatedBy = QueryExtensions.Table<PID_Master>(),
                pid_ProcessedBy = QueryExtensions.Table<PID_Master>(),
                mlu_tran_Site_City = QueryExtensions.Table<MasterLookup>()
            })
                            .LeftJoin(x => x.tran_Site, x => x.br.Trans_DODAAC == x.tran_Site.LOCNCODE)
                            .LeftJoin(x => x.pid_CreatedBy, x => x.br.CreatedByPID == x.pid_CreatedBy.PID)
                            .LeftJoin(x => x.pid_ProcessedBy, x => x.br.IssuedByPID == x.pid_ProcessedBy.PID)
                            .LeftJoin(x => x.mlu_tran_Site_City, x => x.tran_Site.CITY == x.mlu_tran_Site_City.User_Key && "LOCN" == x.mlu_tran_Site_City.LU_Group_Key)
                            .LeftJoin(benchStockRequisitionLines
                                            .GroupBy(y => y.BenchStockDocNum)
                                            .Select(y => new { DocNum = y.Key, LinesRequested = y.Sum(z => z.QtyRequested), LinesIssued = y.Sum(z => z.QtyIssued) }),
                                        (src, jt) => new { src.br, src.tran_Site, src.pid_CreatedBy, src.pid_ProcessedBy, src.mlu_tran_Site_City, bl = jt },
                                        x => x.br.DOC_NUM == x.bl.DocNum
                            )
                            ;
            var finalQuery = q.Select(x => new
            {
                DOC_NUM = x.br.DOC_NUM,
                Trans_DODAAC = x.br.Trans_DODAAC,
                Req_PID = x.br.Req_PID,
                Req_Julian = x.br.Req_Julian,
                Req_Seq = x.br.Req_Seq,
                DocNumDisplay = x.br.DocNumDisplay,
                Status = x.br.Status,
                SubmittedOn = x.br.SubmittedOn,
                SubmittedBy = x.br.SubmittedBy,
                SubmittedByPID = x.br.SubmittedByPID,
                IssuedOn = x.br.IssuedOn,
                IssuedBy = x.br.IssuedBy,
                IssuedByPID = x.br.IssuedByPID,
                Remarks = x.br.Remarks,
                CreatedByPID = x.br.CreatedByPID,
                UpdatedByPID = x.br.UpdatedByPID,
                UIC_Desc = x.tran_Site.LOCNDSCR,
                UIC_City = x.tran_Site.CITY,
                UIC_City_Desc = x.mlu_tran_Site_City.LU_ShortDesc,
                CreatedByPID_Rank = x.pid_CreatedBy.RANK,
                ProcessedByPID_Rank = x.pid_ProcessedBy.RANK,
                LinesRequested = x.bl.LinesRequested,
                LinesIssued = x.bl.LinesIssued,
                LinesOutOfStock = x.br.IssuedOn != null ? x.bl.LinesRequested - x.bl.LinesIssued : null
            });

            string expectedResult = @"
select a_1.DOC_NUM as DOC_NUM, a_1.Trans_DODAAC as Trans_DODAAC, a_1.Req_PID as Req_PID, a_1.Req_Julian as Req_Julian, a_1.Req_Seq as Req_Seq, a_1.DocNumDisplay as DocNumDisplay, a_1.Status as Status, a_1.SubmittedOn as SubmittedOn, a_1.SubmittedBy as SubmittedBy, a_1.SubmittedByPID as SubmittedByPID, a_1.IssuedOn as IssuedOn, a_1.IssuedBy as IssuedBy, a_1.IssuedByPID as IssuedByPID, a_1.Remarks as Remarks, a_1.CreatedByPID as CreatedByPID, a_1.UpdatedByPID as UpdatedByPID, a_2.LOCNDSCR as UIC_Desc, a_2.CITY as UIC_City, a_5.LU_ShortDesc as UIC_City_Desc, a_3.RANK as CreatedByPID_Rank, a_4.RANK as ProcessedByPID_Rank, a_6.LinesRequested as LinesRequested, a_6.LinesIssued as LinesIssued, case when (a_1.IssuedOn is not null) then (a_6.LinesRequested - a_6.LinesIssued) else null end as LinesOutOfStock
	from BenchStock_Requisition as a_1
			left join SiteBase as a_2 on (a_1.Trans_DODAAC = a_2.LOCNCODE)
			left join PID_Master as a_3 on (a_1.CreatedByPID = a_3.PID)
			left join PID_Master as a_4 on (a_1.IssuedByPID = a_4.PID)
			left join MasterLookup as a_5 on ((a_2.CITY = a_5.User_Key) and ('LOCN' = a_5.LU_Group_Key))
			left join (
				select a_7.BenchStockDocNum as DocNum, Sum(a_7.QtyRequested) as LinesRequested, Sum(a_7.QtyIssued) as LinesIssued
				from BenchStock_RequisitionLine as a_7
				group by a_7.BenchStockDocNum
			) as a_6 on (a_1.DOC_NUM = a_6.DocNum)
";
            Test("Bench stock complex query test", finalQuery.Expression, expectedResult);
        }


        [TestMethod()]
        public void Plan_table_complex_query()
        {
            var planTable = new Queryable<PlanTable>(this.queryProvider);
            var q = planTable
                        .Select(
                            x => new
                            {
                                x.ID,
                                x.Date_Time,
                                x.ID_Plan,
                                ID_Plan_Adv_Start = x.ID_Plan ?? planTable.Where(y => y.ID < x.ID && y.ID_Plan != null).Max(y => y.ID).ToString()
                            }
                        )
                        .GroupBy(x => x.ID_Plan_Adv_Start)
                        .Select(x => new
                        {
                            Start_Date = x.Min(y => y.Date_Time),
                            End_Date = x.Max(y => y.Date_Time),
                            Plan_ID = x.Min(y => y.ID_Plan),
                            Count_PlanID = x.Count()
                        }
                        )
                        .OrderBy(x => x.Start_Date)
                        ;

            string expectedResult = @"
select	Min(a_3.Date_Time) as Start_Date, 
        Max(a_3.Date_Time) as End_Date, 
        Min(a_3.ID_Plan) as Plan_ID, 
        Count(1) as Count_PlanID
from	(
	select	a_1.ID as ID, 
            a_1.Date_Time as Date_Time, 
            a_1.ID_Plan as ID_Plan, 
            isnull(
                a_1.ID_Plan, 
                cast(
                        (
		                    select	Max(a_2.ID) as Col1
		                    from	PlanTable as a_2
		                    where	((a_2.ID < a_1.ID) and (a_2.ID_Plan is not null))
	                    ) as NonUnicodeString(max)
                )
            ) as ID_Plan_Adv_Start
	from	PlanTable as a_1
) as a_3
group by a_3.ID_Plan_Adv_Start
order by Start_Date asc
";
            Test("Plan complex query", q.Expression, expectedResult);
        }

    }
}
