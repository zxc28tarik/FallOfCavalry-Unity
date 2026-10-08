#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using FOC.Domain.Campaign;
using FOC.Domain.Characters;
using FOC.Domain.Cities;
using FOC.Domain.Common;
using FOC.Domain.Economy;

namespace FOC.Presentation.Core
{
    public enum CityInspectionTab { Areas, Production, Stocks, Infrastructure }
    public enum ProductionInspectionResult { Unavailable, AreaEmpty, BuildingInactive, InsufficientInput, OutputOverflow, MechanicallyReady }
    public sealed class CityInspectionChoice
    {
        public CityInspectionChoice(string id, string label) { Id = id; Label = label; }
        public string Id { get; }
        public string Label { get; }
    }
    public sealed class CityStockInspection
    {
        public CityStockInspection(string id, string name, long stock, long? demand)
        { Id = id; Name = name; Stock = stock; Demand = demand; }
        public string Id { get; }
        public string Name { get; }
        public long Stock { get; }
        public long? Demand { get; }
        public long? Shortage => Demand.HasValue ? Math.Max(0, Demand.Value - Stock) : (long?)null;
        public string Text => Name + "    Stok: " + CityInspectionText.Number(Stock) + "    Talep: " + (Demand.HasValue ? CityInspectionText.Number(Demand.Value) : "hesaplanamıyor")
            + "    Eksik: " + (Shortage.HasValue ? CityInspectionText.Number(Shortage.Value) : "bilinmiyor");
    }
    public sealed class CityInspectionSnapshot
    {
        public bool Available { get; internal set; }
        public string CityId { get; internal set; } = "";
        public string RecipeId { get; internal set; } = "";
        public CityInspectionTab Tab { get; internal set; }
        public CityAreaType Area { get; internal set; }
        public string Search { get; internal set; } = "";
        public bool ShortagesOnly { get; internal set; }
        public IReadOnlyList<CityInspectionChoice> Cities { get; internal set; } = Array.Empty<CityInspectionChoice>();
        public IReadOnlyList<CityInspectionChoice> Recipes { get; internal set; } = Array.Empty<CityInspectionChoice>();
        public IReadOnlyList<string> Areas { get; internal set; } = Array.Empty<string>();
        public IReadOnlyList<string> Buildings { get; internal set; } = Array.Empty<string>();
        public IReadOnlyList<CityStockInspection> Stocks { get; internal set; } = Array.Empty<CityStockInspection>();
        public IReadOnlyList<string> Infrastructure { get; internal set; } = Array.Empty<string>();
        public string Summary { get; internal set; } = "Bu şehir için kesin bilgi yok.";
        public string ProductionDetail { get; internal set; } = "";
        public ProductionInspectionResult ProductionResult { get; internal set; }
        public string Officials { get; internal set; } = "";
    }
    /// <summary>Ephemeral, read-only inspection. Exact city knowledge is not production/official command authority.</summary>
    public sealed class CityInspectionSession : IDisposable
    {
        private readonly CampaignRuntimeState _campaign;
        private readonly PresentationViewerContext _viewer;
        private string _city = "", _recipe = "", _search = "";
        private CityInspectionTab _tab;
        private CityAreaType _area;
        private bool _shortages, _disposed;
        public CityInspectionSession(CampaignRuntimeState campaign, PresentationViewerContext viewer)
        { _campaign = campaign ?? throw new ArgumentNullException(nameof(campaign)); _viewer = viewer ?? throw new ArgumentNullException(nameof(viewer)); }
        public void OpenCity(string? id) { Check(); if (id != null) _city = id; }
        public void SelectTab(CityInspectionTab tab) { Check(); if (!Enum.IsDefined(typeof(CityInspectionTab), tab)) throw new ArgumentOutOfRangeException(nameof(tab)); _tab = tab; }
        public void SelectArea(CityAreaType area) { Check(); if (!Enum.IsDefined(typeof(CityAreaType), area)) throw new ArgumentOutOfRangeException(nameof(area)); _area = area; }
        public void SelectRecipe(string id) { Check(); _recipe = id ?? ""; }
        public void FilterStocks(string search, bool shortagesOnly) { Check(); _search = search ?? ""; _shortages = shortagesOnly; }
        private bool Known(PresentationEntityKind kind, string id) => _viewer.CanReadExact(new PresentationEntityRef(kind, id));
        public CityInspectionSnapshot Read()
        {
            Check();
            var choices = _campaign.Cities.OrderedCities.Where(x => Known(PresentationEntityKind.City, x.Id.Value)).Select(x => new CityInspectionChoice(x.Id.Value, x.Definition.Name)).ToArray();
            if (_city.Length == 0) _city = choices.FirstOrDefault()?.Id ?? "";
            var s = new CityInspectionSnapshot { CityId = _city, Cities = Array.AsReadOnly(choices), Area = _area, Tab = _tab, Search = _search, ShortagesOnly = _shortages };
            // Never silently substitute a known city for an explicitly requested unknown one.
            if (!choices.Any(x => x.Id == _city)) return s;
            var city = _campaign.Cities.GetRequired(CityId.Create(_city));
            s.Available = true;
            s.Summary = city.Definition.Name + " · Nüfus kaydı: " + CityInspectionText.Number(city.Metrics.PopulationCount) + " (slice ayarı; tarihsel nüfus tahmini değil)\nRefah, düzen, sağlık ve güvenlik: henüz değerlendirilmemiş. Sıfır puan veya bina bonusu değildir.";
            s.Areas = Array.AsReadOnly(city.OrderedAreas.Select(a => CityInspectionText.Area(a.Type) + "\n" + CityInspectionText.Fullness(a.Fullness) + " · " + a.OrderedActiveBuildings.Count + " etkin").ToArray());
            var selected = city.GetRequiredArea(_area);
            s.Buildings = Array.AsReadOnly(selected.Definition.OrderedBuildingPool.Select(b => CityInspectionText.Building(b.Kind) + " — " +
                (b.Status == CityBuildingContentStatus.Removed ? "İçerikten kaldırılmış" : selected.OrderedActiveBuildings.Any(x => x.Id.Equals(b.Id)) ? "Etkin" : selected.OrderedLockedBuildingIds.Contains(b.Id) ? "Kilitli" : "Etkin değil")
                + (b.EffectTags.Count > 0 ? " · Kurum etiketleri tanımlı; sayısal etkileri henüz hesaplanmıyor." : "")).ToArray());
            s.Infrastructure = Array.AsReadOnly(Enum.GetValues(typeof(CityInfrastructureType)).Cast<CityInfrastructureType>().Select(type =>
            {
                var record = city.OrderedInfrastructure.FirstOrDefault(x => x.Type == type);
                return CityInspectionText.Infrastructure(type) + " — " + (record == null ? "Kayıt yok; kurulu değil anlamına gelmez" : !record.Installed ? "Kurulu değil · Durum değerlendirilmemiş" : "Kurulu · " + CityInspectionText.Condition(record.Condition));
            }).ToArray());
            s.Officials = city.OrderedOfficials.Count == 0 ? "Kethüda görev referansı kayıtlı değil." : string.Join("\n", city.OrderedOfficials.Select(o =>
                "Kethüda · Organization görev referansı: " + o.AssignmentId.Value + "\nKurum: " + o.OrganizationId.Value + " · Kişi ve görev durumu bu incelemede çözülmez; şehir bilgisi kişi yetkisi değildir."));
            var market = _campaign.Economy.OrderedMarkets.FirstOrDefault(x => x.CityId.Equals(city.Id));
            if (market == null) { s.ProductionDetail = "Bu şehir için pazar kaydı yok."; return s; }
            var stocks = new List<CityStockInspection>();
            foreach (var good in _campaign.Economy.Goods.OrderedGoods)
            {
                // Mono/Unity and desktop .NET disagree on Turkish collation. Fold I explicitly, then use ordinal search.
                if (SearchKey(good.Name).IndexOf(SearchKey(_search.Trim()), StringComparison.Ordinal) < 0) continue;
                long? demand; try { demand = market.Demand.TotalFor(good.Id); } catch (OverflowException) { demand = null; }
                var row = new CityStockInspection(good.Id.Value, good.Name, market.Stock.QuantityOf(good.Id), demand);
                if (!_shortages || !row.Shortage.HasValue || row.Shortage.Value > 0) stocks.Add(row);
            }
            s.Stocks = stocks.AsReadOnly();
            s.Recipes = Array.AsReadOnly(_campaign.Economy.Recipes.OrderedRecipes.Select(r => new CityInspectionChoice(r.Id.Value, r.Name)).ToArray());
            if (_recipe.Length == 0) _recipe = s.Recipes.FirstOrDefault()?.Id ?? "";
            s.RecipeId = _recipe;
            var recipe = _campaign.Economy.Recipes.OrderedRecipes.FirstOrDefault(r => r.Id.Value == _recipe);
            if (recipe == null) { s.ProductionDetail = "Reçete bulunamadı."; return s; }
            s.ProductionResult = InspectProduction(city, market, recipe);
            string Line(RecipeGoodsLine line, bool input) => _campaign.Economy.Goods.GetRequired(line.GoodId).Name + " × " + CityInspectionText.Number(line.Quantity.Value)
                + " · stok " + CityInspectionText.Number(market.Stock.QuantityOf(line.GoodId)) + (input ? " · eksik " + CityInspectionText.Number(Math.Max(0, line.Quantity.Value - market.Stock.QuantityOf(line.GoodId))) : "");
            s.ProductionDetail = "Gerekli alan: " + CityInspectionText.Area(CityBuildingRules.RequiredArea(recipe.BuildingKind)) + " · Bina: " + CityInspectionText.Building(recipe.BuildingKind)
                + "\nGİRDİ  " + string.Join("  |  ", recipe.OrderedInputs.Select(x => Line(x, true))) + "\nÇIKTI  " + string.Join("  |  ", recipe.OrderedOutputs.Select(x => Line(x, false)));
            return s;
        }
        private static ProductionInspectionResult InspectProduction(CityState city, CityMarketState market, ProductionRecipeDefinition recipe)
        {
            // Match ProductionService.Execute preflight order, including conservative pre-removal output overflow.
            var area = city.GetRequiredArea(CityBuildingRules.RequiredArea(recipe.BuildingKind));
            if (area.Fullness == CityAreaFullness.Empty) return ProductionInspectionResult.AreaEmpty;
            if (!area.OrderedActiveBuildings.Any(b => b.Kind == recipe.BuildingKind && b.Status == CityBuildingContentStatus.Active)) return ProductionInspectionResult.BuildingInactive;
            if (recipe.OrderedInputs.Any(x => market.Stock.QuantityOf(x.GoodId) < x.Quantity.Value)) return ProductionInspectionResult.InsufficientInput;
            if (recipe.OrderedOutputs.Any(x => !market.Stock.CanAdd(x.GoodId, x.Quantity.Value))) return ProductionInspectionResult.OutputOverflow;
            return ProductionInspectionResult.MechanicallyReady;
        }
        public void Dispose() { _disposed = true; }
        private static string SearchKey(string text) => text.Normalize().Replace('ı', 'I').Replace('i', 'İ').ToUpperInvariant();
        private void Check() { if (_disposed) throw new ObjectDisposedException(nameof(CityInspectionSession)); }
    }
    public static class CityInspectionText
    {
        public static string Number(long n) => n.ToString("N0", CultureInfo.GetCultureInfo("tr-TR"));
        public static string Area(CityAreaType v) => new[] { "İç Kale", "Ticaret", "Han ve Kervan", "Konut", "Askerî", "Sağlık", "Üretim ve Zanaat", "Gıda", "Meydan ve Kültür" }[(int)v];
        public static string Fullness(CityAreaFullness v) => new[] { "Boş", "Düşük doluluk", "Yarı dolu", "Tam dolu" }[(int)v];
        public static string Infrastructure(CityInfrastructureType v) => new[] { "Kuyu", "Çeşme", "Sarnıç", "Su kanalı", "Ana taş yol", "İkincil yol", "Köprü / geçit", "Drenaj hattı", "Atık / kanalizasyon hattı" }[(int)v];
        public static string Condition(CityInfrastructureCondition v) => new[] { "Değerlendirilmemiş", "Kullanılabilir", "Yıpranmış", "İşlevsiz" }[(int)v];
        public static string Building(CityBuildingKind v) => new[] {
            "İç kale surları", "Saray", "Mahkeme / kadı makamı", "Cami", "Medrese", "Devlet sicili", "Başkâtip makamı", "Muhafız karargâhı",
            "Pazar", "Bedesten", "Lonca", "Gümrük", "Han", "Tüccar hanı", "Kervansaray", "Yoksul konutları", "Alt-orta konutlar", "Üst-orta konutlar", "Tüccar konakları",
            "Kışla", "Talim alanı", "Cephanelik", "Süvari ahırları", "Muhafız tesisleri", "Kültüre özgü talim", "Darüşşifa", "Büyük hamam", "Eczane",
            "Tabakhane", "Demirci", "Marangoz", "Dokuma atölyesi", "Boyahane", "Sabunhane", "Kâğıt imalathanesi", "Mumcu", "Taş işliği", "Nalbant",
            "Fırın", "Değirmen", "Ambar", "Kasap", "Balıkçılık", "At meydanı", "Panayır", "Anıtsal kamu yapısı" }[(int)v];
        public static string ProductionReason(ProductionInspectionResult v) => v switch {
            ProductionInspectionResult.MechanicallyReady => "UYGUN · Bina ve stok, tek reçete uygulamasına yeterli. Üretim yapılmadı.",
            ProductionInspectionResult.AreaEmpty => "ENGEL · Gerekli şehir alanı boş.",
            ProductionInspectionResult.BuildingInactive => "ENGEL · Uyumlu etkin bina yok.",
            ProductionInspectionResult.InsufficientInput => "ENGEL · Girdi stoku yetersiz.",
            ProductionInspectionResult.OutputOverflow => "ENGEL · Çıktı, stok sayısal sınırını aşar.",
            _ => "Üretim bilgisi mevcut değil." };
    }
}
