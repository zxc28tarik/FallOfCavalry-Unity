#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using FOC.Application.Military;
using FOC.Domain.Campaign;
using FOC.Domain.Characters;
using FOC.Domain.Common;
using FOC.Domain.Military;

namespace FOC.Presentation.Core
{
    public sealed class ArmyPanelChoice
    {
        public ArmyPanelChoice(string id, string label) { Id = id; Label = label; }
        public string Id { get; }
        public string Label { get; }
    }
    public sealed class ArmyPanelSnapshot
    {
        public string ArmyId { get; internal set; } = "";
        public string SourceId { get; internal set; } = "";
        public string DetailId { get; internal set; } = "";
        public string Quantity { get; internal set; } = "1";
        public ArmyOrderKind Kind { get; internal set; }
        public IReadOnlyList<ArmyPanelChoice> Armies { get; internal set; } = Array.Empty<ArmyPanelChoice>();
        public IReadOnlyList<ArmyPanelChoice> Sources { get; internal set; } = Array.Empty<ArmyPanelChoice>();
        public IReadOnlyList<ArmyPanelChoice> Details { get; internal set; } = Array.Empty<ArmyPanelChoice>();
        public string Summary { get; internal set; } = "";
        public string Resources { get; internal set; } = "";
        public string Note { get; internal set; } = "";
        public string Feedback { get; internal set; } = "";
        public ArmyOrderFailure Failure { get; internal set; } = ArmyOrderFailure.Unauthorized;
        public ArmyOrderPreview? Confirmation { get; internal set; }
    }
    /// <summary>Non-persistent screen state; exact knowledge never grants military command authority.</summary>
    public sealed class ArmyPanelSession : IDisposable
    {
        private readonly CampaignRuntimeState _campaign;
        private readonly PresentationViewerContext _viewer;
        private readonly ArmyOrderSession? _orders;
        private string _army = "", _source = "", _detail = "", _quantity = "1", _feedback = "";
        private ArmyOrderKind _kind;
        private ArmyOrderPreview? _confirmation;
        private bool _disposed;
        public ArmyPanelSession(CampaignRuntimeState campaign, PresentationViewerContext viewer)
        {
            _campaign = campaign; _viewer = viewer;
            if (viewer.ControlledIdentity.Kind == PresentationEntityKind.Character)
                _orders = new ArmyOrderSession(campaign, CharacterId.Create(viewer.ControlledIdentity.Id));
        }
        private bool Known(PresentationEntityKind kind, string id) => _viewer.CanReadExact(new PresentationEntityRef(kind, id));
        private bool Actor(string id) => _viewer.ControlledIdentity.Kind == PresentationEntityKind.Character && _viewer.ControlledIdentity.Id == id;
        public void OpenArmy(string? id) { if (id != null && id != _army) Select(id, _kind, _source, _detail, _quantity); }
        public void Select(string army, ArmyOrderKind kind, string source, string detail, string quantity)
        {
            if (_disposed || _army == army && _kind == kind && _source == source && _detail == detail && _quantity == quantity) return;
            Cancel(); _army = army; _kind = kind; _source = source; _detail = detail; _quantity = quantity; _feedback = "";
        }
        public ArmyPanelSnapshot Read()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(ArmyPanelSession));
            var armies = _campaign.Military.Armies.OrderedArmies.Where(x => Known(PresentationEntityKind.Army, x.Id.Value))
                .Select(x => new ArmyPanelChoice(x.Id.Value, x.Name)).ToArray();
            Normalize(ref _army, armies);
            var s = new ArmyPanelSnapshot { ArmyId = _army, Armies = armies, Kind = _kind, Quantity = _quantity, Feedback = _feedback };
            if (_army.Length == 0) { s.Note = "Bu ordu için kesin bilgi yok."; return s; }
            var army = _campaign.Military.Armies.GetRequired(ArmyId.Create(_army));
            var soldiers = _campaign.Soldiers.Soldiers.OrderedSoldiers.Count(x => army.OrderedUnits.Any(u => u.Id.Equals(x.UnitGroupId)));
            s.Summary = "Toplam personel: " + N(army.Headcount) + " · Kalıcı Soldier: " + N(soldiers) + " · Birlik: " + N(army.OrderedUnits.Count)
                + "\nKomutan: " + (army.Commander?.CharacterId.Value ?? "Atanmamış") + " · Konum: " + (army.Location.CityId?.Value ?? army.Location.Kind.ToString());
            s.Resources = string.Join("  |  ", army.OrderedSupplyRequirements.Select(x =>
                _campaign.Economy.Goods.GetRequired(x.GoodId).Name + ": " + N(army.Supply.QuantityOf(x.GoodId)) + " / ihtiyaç " + N(x.RequiredQuantity)))
                + "\n" + (army.Payroll.OrderedObligations.Count == 0 ? "Tanımlı maaş yükümlülüğü yok; ücret oranı eklenmedi."
                    : "Maaş borçları: " + string.Join(" · ", army.Payroll.OrderedObligations.Select(x => x.Id.Value + " = " + N(x.Arrears))));
            switch (_kind)
            {
                case ArmyOrderKind.Recruit:
                    s.Sources = _campaign.Military.RecruitmentSources.OrderedSources.Where(x => Actor(x.Authority.CharacterId.Value) && army.OrderedUnits.Any(u => u.SourceId.Equals(x.Id)))
                        .Select(x => new ArmyPanelChoice(x.Id.Value, x.Id.Value + " · kalan " + N(x.AvailableHeadcount))).ToArray();
                    Normalize(ref _source, s.Sources);
                    s.Details = _campaign.Soldiers.Definitions.OrderedTroops.Where(x => army.OrderedUnits.Any(u => u.SourceId.Value == _source && u.TroopDefinitionId == x.Id.Value))
                        .Select(x => new ArmyPanelChoice(x.Id.Value, x.Name)).ToArray();
                    s.Note = "Sonlu kaynaktan personel kaydı oluşturur. Yeni Soldier, silah, at veya ekipman üretmez. Teçhizatlandırma ayrı kapıdır."; break;
                case ArmyOrderKind.CitySupply:
                    s.Sources = _campaign.Economy.OrderedMarkets.Where(x => Known(PresentationEntityKind.City, x.CityId.Value))
                        .Select(x => new ArmyPanelChoice(x.CityId.Value, _campaign.Cities.GetRequired(x.CityId).Definition.Name)).ToArray();
                    s.Details = Goods(); s.Note = "Aynı şehirdeki gerçek stoktan orduya ikmal aktarımı. Satın alma veya stok üretimi değildir."; break;
                case ArmyOrderKind.CaravanSupply:
                    s.Sources = _campaign.Economy.Caravans.OrderedCaravans.Where(x => Known(PresentationEntityKind.Caravan, x.Id.Value) && Actor(x.ManagerCharacterId.Value))
                        .Select(x => new ArmyPanelChoice(x.Id.Value, x.Id.Value)).ToArray();
                    s.Details = Goods(); s.Note = "Kervan yöneticisi ve ordu komutanı olmalısın; varış şehrinde buluşmalısınız. NPC yükü devredilmez."; break;
                case ArmyOrderKind.Payroll:
                    s.Sources = army.Payroll.OrderedObligations.Where(x => x.FundingSource.Kind == PayrollFundingSourceKind.CityMarket
                        ? Known(PresentationEntityKind.City, x.FundingSource.Id)
                        : Known(PresentationEntityKind.Caravan, x.FundingSource.Id) && _campaign.Economy.Caravans.OrderedCaravans.Any(c => c.Id.Value == x.FundingSource.Id && Actor(c.ManagerCharacterId.Value)))
                        .Select(x => new ArmyPanelChoice(x.Id.Value, x.Id.Value + " · borç " + N(x.Arrears) + " · kaynak " + x.FundingSource.Id)).ToArray();
                    s.Note = "Yalnız mevcut yükümlülüğün belirlenmiş kaynağından ödeme. Yeni borç, ücret veya finansman oluşturulmaz."; break;
            }
            Normalize(ref _source, s.Sources); Normalize(ref _detail, s.Details);
            s.SourceId = _source; s.DetailId = _detail;
            long.TryParse(_quantity, NumberStyles.None, CultureInfo.InvariantCulture, out var quantity);
            s.Failure = _orders?.Inspect(army.Id, _kind, _source, _detail, quantity).Failure ?? ArmyOrderFailure.Unauthorized;
            if (_source.Length > 0 && _detail.Length > 0 && (_kind == ArmyOrderKind.CitySupply || _kind == ArmyOrderKind.CaravanSupply))
            {
                var good = TradeGoodId.Create(_detail);
                var available = _kind == ArmyOrderKind.CitySupply ? _campaign.Economy.GetRequiredMarket(CityId.Create(_source)).Stock.QuantityOf(good)
                    : _campaign.Economy.Caravans.GetRequired(CaravanId.Create(_source)).Cargo.QuantityOf(good);
                s.Resources += "\nSeçili mal — kaynak: " + N(available) + " · orduda: " + N(army.Supply.QuantityOf(good));
            }
            s.Confirmation = _confirmation; return s;
        }
        private ArmyPanelChoice[] Goods() => _campaign.Economy.Goods.OrderedGoods.Select(x => new ArmyPanelChoice(x.Id.Value, x.Name)).ToArray();
        private void Normalize(ref string selected, IReadOnlyList<ArmyPanelChoice> choices)
        { var current = selected; if (!choices.Any(x => x.Id == current)) { var next = choices.FirstOrDefault()?.Id ?? ""; if (next != selected) { Cancel(); selected = next; } } }
        private static string N(long value) => value.ToString("N0", CultureInfo.GetCultureInfo("tr-TR"));
        public void Prepare()
        {
            if (_disposed) return;
            Cancel(); var s = Read();
            if (_orders == null || s.ArmyId.Length == 0) { _feedback = Reason(ArmyOrderFailure.Unauthorized); return; }
            long.TryParse(s.Quantity, NumberStyles.None, CultureInfo.InvariantCulture, out var quantity);
            var p = _orders.Prepare(ArmyId.Create(s.ArmyId), s.Kind, s.SourceId, s.DetailId, quantity);
            _feedback = Reason(p.Failure); if (p.CanExecute) _confirmation = p;
        }
        public PresentationActionResult Confirm()
        {
            if (_disposed || _orders == null || _confirmation == null) return PresentationActionResult.Rejected(Reason(ArmyOrderFailure.ConfirmationUnavailable));
            var p = _confirmation; _confirmation = null; var f = _orders.Confirm(p);
            _feedback = f == ArmyOrderFailure.None ? "İşlem tamamlandı; gerçek kampanya kayıtları güncellendi." : Reason(f);
            return f == ArmyOrderFailure.None ? PresentationActionResult.Success(_feedback) : PresentationActionResult.Rejected(_feedback);
        }
        public void Cancel() { _confirmation = null; _orders?.Cancel(); }
        public void Dispose() { Cancel(); _disposed = true; }
        public static string Reason(ArmyOrderFailure f) => f switch
        {
            ArmyOrderFailure.None => "İşlem uygun; onaydan önce miktar ve kaynağı kontrol et.",
            ArmyOrderFailure.Unauthorized => "Aktif komutanlık / kaynak yönetim yetkin yok.",
            ArmyOrderFailure.Unavailable => "Ordu, aktör, görev veya kaynak bu işleme uygun değil.",
            ArmyOrderFailure.WrongLocation => "Komutan orduyla, ikmal kaynağı orduyla aynı konumda olmalı.",
            ArmyOrderFailure.InvalidQuantity => "Pozitif tam sayı gir.",
            ArmyOrderFailure.InsufficientSource => "Kaynakta yeterli personel / mal yok.",
            ArmyOrderFailure.NoObligation => "Seçilebilir mevcut maaş yükümlülüğü yok.",
            ArmyOrderFailure.InsufficientFunds => "Yükümlülüğün gerçek finansman kaynağında yeterli para yok.",
            ArmyOrderFailure.ExcessPayment => "Ödeme kalan maaş borcunu aşamaz.",
            ArmyOrderFailure.Overflow => "İşlem sayısal güvenlik sınırını aşıyor.",
            ArmyOrderFailure.StalePreview => "Zaman veya kaynak değişti. İşlemi yeniden incele.",
            _ => "Geçerli onay yok; işlem tekrarlanmadı."
        };
    }
}
