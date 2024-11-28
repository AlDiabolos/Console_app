
using Newtonsoft.Json;

class Program
{
    static void Main(string[] args)
    {
       
        var codeRules = new CodeRulesImplementation();

       
        var factory = new CalculateFactory(codeRules);

        
        var payInfo = new PayInfo(factory);
        
      
        List<Timecard_Record> timecards = JsonHelper.ReadJsonString<Timecard_Record>(Input_Dataset.Timecard());
        List<Rate_Table_Row> rateTable = JsonHelper.ReadJsonString<Rate_Table_Row>(Input_Dataset.RateTable());

        payInfo.SummarizePayInfo(timecards, rateTable);
        /*payInfo.TotalPayInfo(timecards, rateTable);*/
    }
}

public static class JsonHelper
{
    public static List<T> ReadJsonString<T>(string jsonString) where T : class
    {
        try
        {
            // Deserialize JSON string into a list of the specified type
            return JsonConvert.DeserializeObject<List<T>>(jsonString);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error occured while deserializing the JSON string: {ex.Message}");
            return null;
        }
    }
}
public interface ICodeRules
{ 
    decimal Code(string rules);
}
public class CodeRulesImplementation : ICodeRules
{
    public decimal Code(string rules)
    {
        switch (rules.ToLower()) 
        {
            case "regular":
                return 1.0m;

            case "overtime":
                return 1.5m;

            case "double time":
                return 2.0m;

            default:
                throw new ArgumentException("Invalid rule type provided", nameof(rules));
        }
    }
}

public abstract class Calculate
{
    
    protected readonly ICodeRules _codeRules;

    protected Calculate(ICodeRules codeRules)
    {
        
        _codeRules = codeRules;
    }

    public abstract object CalculatePay(Timecard_Record timecard, List<Rate_Table_Row> rateTable);
}
public class Calculate_Summarize_Pay_Info : Calculate
{
    public Calculate_Summarize_Pay_Info(ICodeRules codeRules)
        : base(codeRules)
    {
    }
    
    public override Pay_Summary_Record CalculatePay(Timecard_Record timecard, List<Rate_Table_Row> rateTable)
    {
        try
        {
            if (timecard == null)
                throw new ArgumentNullException(nameof(timecard), "Timecard record cannot be null");

            if (rateTable == null || rateTable.Count == 0)
                throw new ArgumentException("Rate table cannot be null or empty", nameof(rateTable));
            
            // Find the matching rate table row

            var matchingRate = rateTable.FirstOrDefault(rate =>
                rate.Job == timecard.Job_Worked && rate.Dept == timecard.Dept_Worked &&
                timecard.Date_Worked >= rate.Effective_Start && timecard.Date_Worked <= rate.Effective_End);

            if (matchingRate != null)
            {
                // If a matching rate is found, use the ratetables's rate
                return new Pay_Summary_Record
                {
                    Employee_Name = timecard.Employee_Name,
                    Employee_Number = timecard.Employee_Number,
                    Earnings_Code = timecard.Earnings_Code,
                    Total_Hours = timecard.Hours,
                    Total_Pay_Amount = _codeRules.Code(timecard.Earnings_Code) * matchingRate.Hourly_Rate * timecard.Hours + timecard.Bonus,
                    Rate_of_Pay = matchingRate.Hourly_Rate,
                    Job = timecard.Job_Worked,
                    Dept = timecard.Dept_Worked,
                };
            }
            else
            {
                // If no matching rate is found, use the timecard's rate
                return new Pay_Summary_Record
                {
                    Employee_Name = timecard.Employee_Name,
                    Employee_Number = timecard.Employee_Number,
                    Earnings_Code = timecard.Earnings_Code,
                    Total_Hours = timecard.Hours,
                    Total_Pay_Amount = _codeRules.Code(timecard.Earnings_Code) * timecard.Rate * timecard.Hours + timecard.Bonus,
                    Rate_of_Pay = timecard.Rate,
                    Job = timecard.Job_Worked,
                    Dept = timecard.Dept_Worked,
                };
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in CalculatePay: {ex.Message}");
            throw;
        }
    }
   
}
public interface ICalculateFactory
{
    Calculate CreateCalculate(string type);
}

public class CalculateFactory : ICalculateFactory
{
   
    private readonly ICodeRules _codeRules;

    public CalculateFactory(ICodeRules codeRules)
    {
        _codeRules = codeRules;
    }

    public Calculate CreateCalculate(string type)
    {
        try
        {
            return type switch
            {
                "Summarize" => new Calculate_Summarize_Pay_Info(_codeRules),
                /*"Total" => new Calculate_Total_Pay_Info(_validate, _codeRules),*/
                _ => throw new ArgumentException($"Unknown Calculate type: {type}")
            };
        }

        catch (Exception ex)
        {
            Console.WriteLine($"Error in CalculateFactory: {ex.Message}");
            throw;
        }
    }
}

public class PayInfo
{
    private readonly ICalculateFactory _factory;

    public PayInfo(ICalculateFactory factory)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory), "Factory cannot be null");
    }

    public void SummarizePayInfo(List<Timecard_Record> timecards, List<Rate_Table_Row> rateTable)
    {
        try
        {
            if (timecards == null || timecards.Count == 0)
                throw new ArgumentException("Timecard list cannot be null or empty", nameof(timecards));

            if (rateTable == null || rateTable.Count == 0)
                throw new ArgumentException("Rate table cannot be null or empty", nameof(rateTable));

            var calculate = _factory.CreateCalculate("Summarize");

            List<Pay_Summary_Record> result = new List<Pay_Summary_Record>();

            foreach (var timecard_record in timecards)
            {
                try
                {
                    var paySummary = calculate.CalculatePay(timecard_record, rateTable) as Pay_Summary_Record;

                    if (paySummary != null)
                    {
                        var matchingRecord = result.FirstOrDefault(existing =>
                            existing.Employee_Number == paySummary.Employee_Number &&
                            existing.Earnings_Code == paySummary.Earnings_Code &&
                            existing.Rate_of_Pay == paySummary.Rate_of_Pay &&
                            existing.Job == paySummary.Job &&
                            existing.Dept == paySummary.Dept);

                        if (matchingRecord != null)
                        {
                            matchingRecord.Total_Pay_Amount += paySummary.Total_Pay_Amount;
                            matchingRecord.Total_Hours += paySummary.Total_Hours;
                        }
                        else
                        {
                            result.Add(paySummary);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(
                        $"Error processing timecard record for employee {timecard_record.Employee_Number}: {ex.Message}");
                }
            }


            var employeeSummaries = result
    .GroupBy(r => r.Employee_Number)
    .Select(employeeGroup => new Employee_Summary
    {
        Employee_Number = employeeGroup.Key,
        Employee_Name = employeeGroup.First().Employee_Name, 
        totalpay = employeeGroup.Sum(x => x.Total_Pay_Amount),
        hours = employeeGroup.Sum(x => x.Total_Hours),
        JobDetails = employeeGroup
            .GroupBy(r => new { r.Job, r.Dept, r.Earnings_Code, r.Rate_of_Pay })
            .Select(jobGroup => new JobDeptEarnings_Summary
            {
                Job = jobGroup.Key.Job,
                Dept = jobGroup.Key.Dept,
                Earnings_Code = jobGroup.Key.Earnings_Code,
                Rate_of_Pay = jobGroup.Key.Rate_of_Pay,
                Total_Hours = jobGroup.Sum(x => x.Total_Hours),
                Total_Pay_Amount = jobGroup.Sum(x => x.Total_Pay_Amount)
            })
            .ToList()
    })
    .ToList();

            // Displaying grouping results
            foreach (var summary in employeeSummaries)
            {
                Console.WriteLine(" ");
                Console.WriteLine($"Employee number: {summary.Employee_Number}");
                Console.WriteLine($"Employee name: {summary.Employee_Name}");
                Console.WriteLine($"Total Pay: {summary.totalpay}, Total Hours: {summary.hours}");
                Console.WriteLine("Job Details:");
                foreach (var detail in summary.JobDetails)
                {
                    Console.WriteLine(
                        $"  Job: {detail.Job}, Dept: {detail.Dept}, Earnings Code: {detail.Earnings_Code}, Rate of Pay: {detail.Rate_of_Pay}");
                    Console.WriteLine($"    Total Hours: {detail.Total_Hours}, Total Pay: {detail.Total_Pay_Amount}");
                }

                Console.WriteLine();
            }
            
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in SummarizePayInfo: {ex.Message}");
        }
    }
}
public class Timecard_Record 
{
public string Employee_Name {get; set;}
public string Employee_Number {get; set;}
public DateTime Date_Worked {get; set;}
public string Job_Worked {get; set;}
public string Dept_Worked {get; set;}
public string Earnings_Code {get; set;}
public decimal Hours {get; set;}
public decimal Rate {get; set;}
public decimal Bonus {get; set;}
}
public class Rate_Table_Row 
{
public string Job {get; set;}
public string Dept {get; set;}
public DateTime Effective_Start {get; set;}
public DateTime Effective_End {get; set;}
public decimal Hourly_Rate {get; set;}
}
public class Pay_Summary_Record 
{
public string Employee_Name {get; set;}
public string Employee_Number {get; set;}
public string Earnings_Code {get; set;}
public decimal Total_Hours {get; set;}
public decimal Total_Pay_Amount {get; set;}
public decimal Rate_of_Pay {get; set;}
public string Job {get; set; }
public string Dept {get; set;}
}

public class JobDeptEarnings_Summary
{
    public string Job { get; set; }
    public string Dept { get; set; }
    public string Earnings_Code { get; set; }
    public decimal Rate_of_Pay { get; set; }
    public decimal Total_Hours { get; set; }
    public decimal Total_Pay_Amount { get; set; }
}


public class Employee_Summary
{
    public string Employee_Number { get; set; }
    public string Employee_Name { get; set; }
    public List<JobDeptEarnings_Summary> JobDetails { get; set; }
    public decimal totalpay { get; set; }
    public decimal hours { get; set; }
}
public static class Input_Dataset
{
    public static string RateTable()
    {
    return @"[
  {
    ""Job"": ""Laborer"",
    ""Dept"": ""001"",
    ""Effective_Start"": ""1/1/2023"",
    ""Effective_End"": ""1/1/2024"",
    ""Hourly_Rate"": 18.75
  },
  {
    ""Job"": ""Laborer"",
    ""Dept"": ""002"",
    ""Effective_Start"": ""1/1/2023"",
    ""Effective_End"": ""1/1/2024"",
    ""Hourly_Rate"": 17.85
  },
  {
    ""Job"": ""Scrapper"",
    ""Dept"": ""001"",
    ""Effective_Start"": ""1/3/2022"",
    ""Effective_End"": ""1/3/2023"",
    ""Hourly_Rate"": 19.45
  },
  {
    ""Job"": ""Scrapper"",
    ""Dept"": ""001"",
    ""Effective_Start"": ""1/4/2023"",
    ""Effective_End"": ""1/1/2024"",
    ""Hourly_Rate"": 20.45
  },
  {
    ""Job"": ""Scrapper"",
    ""Dept"": ""002"",
    ""Effective_Start"": ""1/3/2022"",
    ""Effective_End"": ""1/3/2023"",
    ""Hourly_Rate"": 20.55
  },
  {
    ""Job"": ""Scrapper"",
    ""Dept"": ""002"",
    ""Effective_Start"": ""1/4/2023"",
    ""Effective_End"": ""1/1/2024"",
    ""Hourly_Rate"": 21.60
  },
  {
    ""Job"": ""Scrapper"",
    ""Dept"": ""003"",
    ""Effective_Start"": ""1/3/2022"",
    ""Effective_End"": ""1/3/2023"",
    ""Hourly_Rate"": 22.15
  },
  {
    ""Job"": ""Scrapper"",
    ""Dept"": ""003"",
    ""Effective_Start"": ""1/4/2023"",
    ""Effective_End"": ""1/1/2024"",
    ""Hourly_Rate"": 24.15
  },
  {
    ""Job"": ""Foreman"",
    ""Dept"": ""001"",
    ""Effective_Start"": ""1/1/2023"",
    ""Effective_End"": ""1/1/2024"",
    ""Hourly_Rate"": 13.55
  },
  {
    ""Job"": ""Foreman"",
    ""Dept"": ""002"",
    ""Effective_Start"": ""1/1/2023"",
    ""Effective_End"": ""1/1/2024"",
    ""Hourly_Rate"": 14.50
  },
  {
    ""Job"": ""Foreman"",
    ""Dept"": ""003"",
    ""Effective_Start"": ""1/1/2023"",
    ""Effective_End"": ""1/1/2024"",
    ""Hourly_Rate"": 15.60
  }
]";
    }

    public static string Timecard()
    {
        return @"[
  {
    ""Employee_Name"": ""Kyle James"",
    ""Employee_Number"": ""110011"",
    ""Date_Worked"": ""1/1/2023"",
    ""Earnings_Code"": ""Regular"",
    ""Job_Worked"": ""Laborer"",
    ""Dept_Worked"": ""001"",
    ""Hours"": 8,
    ""Rate"": 15.5,
    ""Bonus"": 0
  },
  {
    ""Employee_Name"": ""Kyle James"",
    ""Employee_Number"": ""110011"",
    ""Date_Worked"": ""1/2/2023"",
    ""Earnings_Code"": ""Regular"",
    ""Job_Worked"": ""Laborer"",
    ""Dept_Worked"": ""001"",
    ""Hours"": 8,
    ""Rate"": 15.5,
    ""Bonus"": 0
  },
  {
    ""Employee_Name"": ""Kyle James"",
    ""Employee_Number"": ""110011"",
    ""Date_Worked"": ""1/3/2023"",
    ""Earnings_Code"": ""Regular"",
    ""Job_Worked"": ""Laborer"",
    ""Dept_Worked"": ""001"",
    ""Hours"": 8,
    ""Rate"": 15.5,
    ""Bonus"": 0
  },
  {
    ""Employee_Name"": ""Kyle James"",
    ""Employee_Number"": ""110011"",
    ""Date_Worked"": ""1/4/2023"",
    ""Earnings_Code"": ""Regular"",
    ""Job_Worked"": ""Laborer"",
    ""Dept_Worked"": ""001"",
    ""Hours"": 8,
    ""Rate"": 15.5,
    ""Bonus"": 0
  },
  {
    ""Employee_Name"": ""Kyle James"",
    ""Employee_Number"": ""110011"",
    ""Date_Worked"": ""1/5/2023"",
    ""Earnings_Code"": ""Regular"",
    ""Job_Worked"": ""Laborer"",
    ""Dept_Worked"": ""001"",
    ""Hours"": 8,
    ""Rate"": 15.5,
    ""Bonus"": 0
  },
  {
    ""Employee_Name"": ""Kyle James"",
    ""Employee_Number"": ""110011"",
    ""Date_Worked"": ""1/6/2023"",
    ""Earnings_Code"": ""Overtime"",
    ""Job_Worked"": ""Laborer"",
    ""Dept_Worked"": ""001"",
    ""Hours"": 8,
    ""Rate"": 15.5,
    ""Bonus"": 0
  },
  {
    ""Employee_Name"": ""Jane Smith"",
    ""Employee_Number"": ""120987"",
    ""Date_Worked"": ""1/1/2023"",
    ""Earnings_Code"": ""Regular"",
    ""Job_Worked"": ""Scrapper"",
    ""Dept_Worked"": ""002"",
    ""Hours"": 10,
    ""Rate"": 17.65,
    ""Bonus"": 0
  },
  {
    ""Employee_Name"": ""Jane Smith"",
    ""Employee_Number"": ""120987"",
    ""Date_Worked"": ""1/2/2023"",
    ""Earnings_Code"": ""Regular"",
    ""Job_Worked"": ""Scrapper"",
    ""Dept_Worked"": ""002"",
    ""Hours"": 10,
    ""Rate"": 17.65,
    ""Bonus"": 0
  },
  {
    ""Employee_Name"": ""Jane Smith"",
    ""Employee_Number"": ""120987"",
    ""Date_Worked"": ""1/3/2023"",
    ""Earnings_Code"": ""Regular"",
    ""Job_Worked"": ""Scrapper"",
    ""Dept_Worked"": ""002"",
    ""Hours"": 10,
    ""Rate"": 17.65,
    ""Bonus"": 0
  },
  {
    ""Employee_Name"": ""Jane Smith"",
    ""Employee_Number"": ""120987"",
    ""Date_Worked"": ""1/4/2023"",
    ""Earnings_Code"": ""Regular"",
    ""Job_Worked"": ""Scrapper"",
    ""Dept_Worked"": ""004"",
    ""Hours"": 10,
    ""Rate"": 17.65,
    ""Bonus"": 0
  },
  {
    ""Employee_Name"": ""Jane Smith"",
    ""Employee_Number"": ""120987"",
    ""Date_Worked"": ""1/5/2023"",
    ""Earnings_Code"": ""Overtime"",
    ""Job_Worked"": ""Scrapper"",
    ""Dept_Worked"": ""004"",
    ""Hours"": 6,
    ""Rate"": 17.65,
    ""Bonus"": 0
  },
  {
    ""Employee_Name"": ""Jane Smith"",
    ""Employee_Number"": ""120987"",
    ""Date_Worked"": ""1/6/2023"",
    ""Earnings_Code"": ""Overtime"",
    ""Job_Worked"": ""Scrapper"",
    ""Dept_Worked"": ""004"",
    ""Hours"": 6,
    ""Rate"": 17.65,
    ""Bonus"": 0
  },
  {
    ""Employee_Name"": ""Jane Smith"",
    ""Employee_Number"": ""120987"",
    ""Date_Worked"": ""1/7/2023"",
    ""Earnings_Code"": ""Double time"",
    ""Job_Worked"": ""Scrapper"",
    ""Dept_Worked"": ""004"",
    ""Hours"": 5,
    ""Rate"": 17.65,
    ""Bonus"": 0
  },
  {
    ""Employee_Name"": ""Amy Penn"",
    ""Employee_Number"": ""100002"",
    ""Date_Worked"": ""1/1/2023"",
    ""Earnings_Code"": ""Regular"",
    ""Job_Worked"": ""Foreman"",
    ""Dept_Worked"": ""003"",
    ""Hours"": 8,
    ""Rate"": 17.75,
    ""Bonus"": 0
  },
  {
    ""Employee_Name"": ""Amy Penn"",
    ""Employee_Number"": ""100002"",
    ""Date_Worked"": ""1/2/2023"",
    ""Earnings_Code"": ""Regular"",
    ""Job_Worked"": ""Foreman"",
    ""Dept_Worked"": ""003"",
    ""Hours"": 12,
    ""Rate"": 17.75,
    ""Bonus"": 0
  },
  {
    ""Employee_Name"": ""Amy Penn"",
    ""Employee_Number"": ""100002"",
    ""Date_Worked"": ""1/3/2023"",
    ""Earnings_Code"": ""Regular"",
    ""Job_Worked"": ""Foreman"",
    ""Dept_Worked"": ""003"",
    ""Hours"": 10,
    ""Rate"": 17.75,
    ""Bonus"": 0
  },
  {
    ""Employee_Name"": ""Amy Penn"",
    ""Employee_Number"": ""100002"",
    ""Date_Worked"": ""1/4/2023"",
    ""Earnings_Code"": ""Regular"",
    ""Job_Worked"": ""Foreman"",
    ""Dept_Worked"": ""003"",
    ""Hours"": 10,
    ""Rate"": 17.75,
    ""Bonus"": 0
  },
  {
    ""Employee_Name"": ""Amy Penn"",
    ""Employee_Number"": ""100002"",
    ""Date_Worked"": ""1/5/2023"",
    ""Earnings_Code"": ""Overtime"",
    ""Job_Worked"": ""Foreman"",
    ""Dept_Worked"": ""003"",
    ""Hours"": 5,
    ""Rate"": 17.75,
    ""Bonus"": 125
  },
  {
    ""Employee_Name"": ""Amy Penn"",
    ""Employee_Number"": ""100002"",
    ""Date_Worked"": ""1/6/2023"",
    ""Earnings_Code"": ""Overtime"",
    ""Job_Worked"": ""Foreman"",
    ""Dept_Worked"": ""003"",
    ""Hours"": 5,
    ""Rate"": 17.75,
    ""Bonus"": 175
  }
]";
    }
    
}



