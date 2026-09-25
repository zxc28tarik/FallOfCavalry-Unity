#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace FOC.Application.HistoricalContent
{
    public enum HistoricalContentTruth { HistoricalAttested, HistoricalReconstruction, SliceFiction, SliceTuning }

    public sealed class HistoricalCharacterRecord
    {
        public HistoricalCharacterRecord(string id,string name,HistoricalContentTruth truth,string role,string importance,string cityId,string sourceId,string confidence,HistoricalContentTruth statsTruth,int[] stats)
        { Id=id;Name=name;Truth=truth;Role=role;Importance=importance;CityId=cityId;SourceId=sourceId;Confidence=confidence;StatsTruth=statsTruth;Stats=stats; }
        public string Id{get;} public string Name{get;} public HistoricalContentTruth Truth{get;} public string Role{get;} public string Importance{get;} public string CityId{get;} public string SourceId{get;} public string Confidence{get;} public HistoricalContentTruth StatsTruth{get;} public int[] Stats{get;}
    }
    public sealed class HistoricalCityRecord
    {
        public HistoricalCityRecord(string id,string name,string sourceId,HistoricalContentTruth truth,IReadOnlyList<string> buildings,IReadOnlyList<string> infrastructure,HistoricalContentTruth metricsTruth)
        {Id=id;Name=name;SourceId=sourceId;Truth=truth;Buildings=buildings;Infrastructure=infrastructure;MetricsTruth=metricsTruth;}
        public string Id{get;}public string Name{get;}public string SourceId{get;}public HistoricalContentTruth Truth{get;}public IReadOnlyList<string> Buildings{get;}public IReadOnlyList<string> Infrastructure{get;}public HistoricalContentTruth MetricsTruth{get;}
    }
    public sealed class HistoricalGoodRecord
    {
        public HistoricalGoodRecord(string id,string name,string category,long weight,bool food,bool military,bool luxury,long value,string sourceId,HistoricalContentTruth truth,HistoricalContentTruth numericTruth)
        {Id=id;Name=name;Category=category;Weight=weight;Food=food;Military=military;Luxury=luxury;Value=value;SourceId=sourceId;Truth=truth;NumericTruth=numericTruth;}
        public string Id{get;}public string Name{get;}public string Category{get;}public long Weight{get;}public bool Food{get;}public bool Military{get;}public bool Luxury{get;}public long Value{get;}public string SourceId{get;}public HistoricalContentTruth Truth{get;}public HistoricalContentTruth NumericTruth{get;}
    }
    public sealed class HistoricalRecipeRecord
    {
        public HistoricalRecipeRecord(string id,string name,string building,string inputs,string outputs,string sourceId,HistoricalContentTruth truth,HistoricalContentTruth numericTruth)
        {Id=id;Name=name;Building=building;Inputs=inputs;Outputs=outputs;SourceId=sourceId;Truth=truth;NumericTruth=numericTruth;}
        public string Id{get;}public string Name{get;}public string Building{get;}public string Inputs{get;}public string Outputs{get;}public string SourceId{get;}public HistoricalContentTruth Truth{get;}public HistoricalContentTruth NumericTruth{get;}
    }
    public sealed class HistoricalStockRecord
    {
        public HistoricalStockRecord(string cityId,string goodId,long quantity,long demand,HistoricalContentTruth numericTruth){CityId=cityId;GoodId=goodId;Quantity=quantity;Demand=demand;NumericTruth=numericTruth;}
        public string CityId{get;}public string GoodId{get;}public long Quantity{get;}public long Demand{get;}public HistoricalContentTruth NumericTruth{get;}
    }
    public sealed class HistoricalSliceContent
    {
        public string ContentVersion{get;internal set;}=string.Empty;public string AnchorDate{get;internal set;}=string.Empty;
        public SortedSet<string> SourceIds{get;}=new SortedSet<string>(StringComparer.Ordinal);
        public List<HistoricalCharacterRecord> Characters{get;}=new List<HistoricalCharacterRecord>();
        public List<HistoricalCityRecord> Cities{get;}=new List<HistoricalCityRecord>();
        public List<HistoricalGoodRecord> Goods{get;}=new List<HistoricalGoodRecord>();
        public List<HistoricalRecipeRecord> Recipes{get;}=new List<HistoricalRecipeRecord>();
        public List<HistoricalStockRecord> Stocks{get;}=new List<HistoricalStockRecord>();
    }

    public sealed class HistoricalSliceContentLoader
    {
        public HistoricalSliceContent Load(string text)
        {
            if(string.IsNullOrWhiteSpace(text))throw new ArgumentException("Historical slice content is required.",nameof(text));
            var result=new HistoricalSliceContent();var lineNumber=0;
            foreach(var raw in text.Replace("\r",string.Empty).Split('\n'))
            {
                lineNumber++;var line=raw.Trim();if(line.Length==0||line.StartsWith("#",StringComparison.Ordinal))continue;var p=line.Split('|');
                try
                {
                    switch(p[0])
                    {
                        case "META":Require(p,3);result.ContentVersion=Text(p[1]);result.AnchorDate=Text(p[2]);break;
                        case "SOURCE":Require(p,3);result.SourceIds.Add(Text(p[1]));break;
                        case "CHAR":Require(p,11);var stats=p[10].Split(',').Select(x=>int.Parse(x,CultureInfo.InvariantCulture)).ToArray();if(stats.Length!=9)throw new FormatException("Character needs nine stats.");result.Characters.Add(new HistoricalCharacterRecord(Text(p[1]),Text(p[2]),Truth(p[3]),Text(p[4]),Text(p[5]),Text(p[6]),Text(p[7]),Text(p[8]),Truth(p[9]),stats));break;
                        case "CITY":Require(p,8);result.Cities.Add(new HistoricalCityRecord(Text(p[1]),Text(p[2]),Text(p[3]),Truth(p[4]),List(p[5]),List(p[6]),Truth(p[7])));break;
                        case "GOOD":Require(p,13);result.Goods.Add(new HistoricalGoodRecord(Text(p[1]),Text(p[2]),Text(p[3]),Long(p[4]),Bool(p[5]),Bool(p[6]),Bool(p[7]),Long(p[8]),Text(p[9]),Truth(p[10]),Truth(p[11])));break;
                        case "RECIPE":Require(p,9);result.Recipes.Add(new HistoricalRecipeRecord(Text(p[1]),Text(p[2]),Text(p[3]),Text(p[4]),Text(p[5]),Text(p[6]),Truth(p[7]),Truth(p[8])));break;
                        case "STOCK":Require(p,6);result.Stocks.Add(new HistoricalStockRecord(Text(p[1]),Text(p[2]),Long(p[3]),Long(p[4]),Truth(p[5])));break;
                        default:throw new FormatException("Unknown record type: "+p[0]);
                    }
                }catch(Exception exception){throw new FormatException("Historical content line "+lineNumber+" is invalid: "+exception.Message,exception);}
            }
            var errors=new HistoricalSliceContentValidator().Validate(result);if(errors.Count>0)throw new InvalidOperationException(string.Join("; ",errors));return result;
        }
        private static void Require(string[] p,int minimum){if(p.Length<minimum)throw new FormatException("Record has too few columns.");}
        private static string Text(string value){value=value.Trim();if(value.Length==0)throw new FormatException("Required text is empty.");return value;}
        private static long Long(string value)=>long.Parse(value,CultureInfo.InvariantCulture);
        private static bool Bool(string value)=>bool.Parse(value);
        private static HistoricalContentTruth Truth(string value)=>(HistoricalContentTruth)Enum.Parse(typeof(HistoricalContentTruth),value,false);
        private static IReadOnlyList<string> List(string value)=>value.Length==0?Array.Empty<string>():value.Split(';').Select(Text).ToArray();
    }

    public sealed class HistoricalSliceContentValidator
    {
        private static readonly string[] Forbidden={"city-home","city-other","commander-a","commander-b","army-a","army-b","PROOF_ONLY"};
        public IReadOnlyList<string> Validate(HistoricalSliceContent content)
        {
            var errors=new List<string>();if(content==null){errors.Add("Content is null.");return errors;}if(string.IsNullOrWhiteSpace(content.ContentVersion))errors.Add("Content version is missing.");
            if(!DateTime.TryParseExact(content.AnchorDate,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out var date)||date.Year<1648||date.Year>1650)errors.Add("Anchor date must be within 1648-1650.");
            Unique(content.Characters.Select(x=>x.Id),"character",errors);Unique(content.Cities.Select(x=>x.Id),"city",errors);Unique(content.Goods.Select(x=>x.Id),"good",errors);Unique(content.Recipes.Select(x=>x.Id),"recipe",errors);
            var cities=new HashSet<string>(content.Cities.Select(x=>x.Id),StringComparer.Ordinal);var goods=new HashSet<string>(content.Goods.Select(x=>x.Id),StringComparer.Ordinal);
            foreach(var x in content.Characters){if(!cities.Contains(x.CityId))errors.Add("Character city missing: "+x.Id);if(!content.SourceIds.Contains(x.SourceId))errors.Add("Character source missing: "+x.Id);if(x.StatsTruth!=HistoricalContentTruth.SliceTuning)errors.Add("Character stats must be SliceTuning: "+x.Id);if(x.Stats.Any(v=>v<0||v>100))errors.Add("Character SliceTuning stat out of range: "+x.Id);}
            foreach(var x in content.Cities){if(!content.SourceIds.Contains(x.SourceId))errors.Add("City source missing: "+x.Id);if(x.MetricsTruth!=HistoricalContentTruth.SliceTuning)errors.Add("City metrics must be SliceTuning: "+x.Id);}
            foreach(var x in content.Goods){if(!content.SourceIds.Contains(x.SourceId))errors.Add("Good source missing: "+x.Id);if(x.NumericTruth!=HistoricalContentTruth.SliceTuning)errors.Add("Economic numbers must be SliceTuning: "+x.Id);}
            foreach(var x in content.Recipes){if(!content.SourceIds.Contains(x.SourceId))errors.Add("Recipe source missing: "+x.Id);if(x.NumericTruth!=HistoricalContentTruth.SliceTuning)errors.Add("Recipe quantities must be SliceTuning: "+x.Id);foreach(var id in Goods(x.Inputs).Concat(Goods(x.Outputs)))if(!goods.Contains(id))errors.Add("Recipe good missing: "+x.Id+"/"+id);}
            foreach(var x in content.Stocks){if(!cities.Contains(x.CityId)||!goods.Contains(x.GoodId))errors.Add("Stock reference missing: "+x.CityId+"/"+x.GoodId);if(x.NumericTruth!=HistoricalContentTruth.SliceTuning)errors.Add("Stock values must be SliceTuning: "+x.CityId+"/"+x.GoodId);if(x.Quantity<0||x.Demand<0)errors.Add("Stock tuning cannot be negative.");}
            var canonical=string.Join("\n",content.Characters.Select(x=>x.Id).Concat(content.Cities.Select(x=>x.Id)));foreach(var forbidden in Forbidden)if(canonical.IndexOf(forbidden,StringComparison.OrdinalIgnoreCase)>=0)errors.Add("Forbidden proof identity: "+forbidden);
            return errors;
        }
        private static IEnumerable<string> Goods(string spec)=>spec.Split(';').Select(x=>x.Split(':')[0]);
        private static void Unique(IEnumerable<string> values,string label,List<string> errors){var set=new HashSet<string>(StringComparer.Ordinal);foreach(var value in values)if(!set.Add(value))errors.Add("Duplicate "+label+" ID: "+value);}
    }
}
