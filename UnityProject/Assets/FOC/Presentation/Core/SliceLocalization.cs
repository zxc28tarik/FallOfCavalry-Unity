using System;
using System.Collections.Generic;
using System.Globalization;

namespace FOC.Presentation.Core
{
    public static class SliceLocalization
    {
        public static IReadOnlyDictionary<string,string> Turkish { get; } = new Dictionary<string,string>
        {
            ["presentation.reports.delivered"]="Teslim edilmiş rapor",
            ["presentation.city.area.innercastle"]="İç Kale", ["presentation.city.area.trade"]="Ticaret",
            ["presentation.city.area.inncaravan"]="Han ve Kervan", ["presentation.city.area.housing"]="Konut",
            ["presentation.city.area.military"]="Askerî", ["presentation.city.area.health"]="Sağlık",
            ["presentation.city.area.productioncraft"]="Üretim ve Zanaat", ["presentation.city.area.foodsupply"]="Gıda",
            ["presentation.city.area.squareculture"]="Meydan ve Kültür",
            ["presentation.city.fullness.empty"]="Boş", ["presentation.city.fullness.low"]="Düşük doluluk",
            ["presentation.city.fullness.half"]="Yarı dolu", ["presentation.city.fullness.full"]="Tam dolu",
            ["presentation.trade.reason.none"]="İşlem koşulları uygun; onaydan önce tekrar denetlenir.",
            ["presentation.trade.reason.unauthorized"]="Bu kervanın yöneticisi değilsin. Bilgiyi görmek işlem yetkisi vermez.",
            ["presentation.trade.reason.unavailable"]="İşlem için gerekli aktör, mal veya etkin kervan bulunamadı.",
            ["presentation.trade.reason.invalidquantity"]="Miktar pozitif tam sayı olmalı.",
            ["presentation.trade.reason.priceunavailable"]="Bu mal için içerikte onaylı referans fiyat yok.",
            ["presentation.trade.reason.wronglocation"]="Alım çıkış pazarında, satış varış pazarında yapılır; kervan o pazarda olmalı.",
            ["presentation.trade.reason.insufficientstock"]="Şehirde yeterli mal yok.",
            ["presentation.trade.reason.insufficientcargo"]="Kervanda satılacak miktar kadar mal yok.",
            ["presentation.trade.reason.capacity"]="Kervanın yük kapasitesi yetersiz.",
            ["presentation.trade.reason.caravanfunds"]="Kervanın işletme parası yetersiz.",
            ["presentation.trade.reason.marketfunds"]="Pazarın ödeme için parası yetersiz.",
            ["presentation.trade.reason.overflow"]="Miktar, toplam veya muhasebe sınırı aşılıyor.",
            ["presentation.trade.reason.stalequote"]="Onay beklerken stok, yük veya para değişti. İşlemi yeniden incele.",
            ["presentation.trade.reason.confirmationunavailable"]="Geçerli bekleyen onay yok; eski onay tekrar kullanılamaz.",
            ["presentation.trade.stage.atorigin"]="Çıkış pazarında",
            ["presentation.trade.stage.intransit"]="Yolda",
            ["presentation.trade.stage.atdestination"]="Varış pazarında",
            ["presentation.trade.completed"]="İşlem tamamlandı. Stok, para ve kervan muhasebesi güncellendi.",
            ["presentation.map.traveller"]="Yolcu",
            ["presentation.travel.select-destination"]="Haritadan veya listeden gidilecek yeri seç.",
            ["presentation.travel.preview-ready"]="Rota hazır. Yola çık emri verilene kadar zaman ilerlemez.",
            ["presentation.travel.in-progress"]="Yolculuk sürüyor. İlerlettiğin zaman mevcut yolculuk servisine uygulanır.",
            ["presentation.travel.paused"]="Kampanya saati duraklatılmış; zaman ilerletilemiyor.",
            ["presentation.travel.blocked.actorunavailable"]="Denetlediğin bir yolcu bulunamadı.",
            ["presentation.travel.blocked.dead"]="Hayatta olmayan karakter yola çıkamaz.",
            ["presentation.travel.blocked.captive"]="Esir karakter yola çıkamaz.",
            ["presentation.travel.blocked.alreadytravelling"]="Yolculuk bitmeden yeni bir yolculuk başlatılamaz.",
            ["presentation.travel.blocked.originunavailable"]="Karakter bilinen bir hareket noktasında değil.",
            ["presentation.travel.blocked.destinationunavailable"]="Seçilen hedef mevcut yol ağında bulunmuyor.",
            ["presentation.travel.blocked.alreadyatdestination"]="Zaten bu konumdasın. Başka bir hedef seç.",
            ["presentation.travel.blocked.noroute"]="Bu hedefe mevcut yol ağı üzerinden ulaşılamıyor.",
            ["presentation.action.no-active-journey"]="İlerletilecek kendi yolculuğun yok.",
            ["presentation.action.advance-one-hour"]="1 saat ilerlet",
            ["presentation.action.already-travelling"]="Yolculuk zaten sürüyor.",
            ["presentation.route-mode.road"]="Kara yolu", ["presentation.route-mode.sea"]="Deniz yolu", ["presentation.route-mode.crossing"]="Geçiş",
            ["presentation.screen.map"]="Marmara Haritası",["presentation.screen.city"]="Şehir",["presentation.screen.character"]="Karakter",["presentation.screen.organization"]="Teşkilat",["presentation.screen.trade"]="Ticaret",["presentation.screen.army"]="Ordu",["presentation.screen.diplomacy"]="Diplomasi",["presentation.screen.battle"]="Muharebe",["presentation.screen.reports"]="Raporlar",["presentation.screen.ledger"]="Hesap Defteri",["presentation.screen.encounter-contract"]="Karşılaşma ve Sözleşme",
            ["presentation.section.current"]="Mevcut Durum",["presentation.section.details"]="Ayrıntılar",["presentation.section.trend"]="Eğilim",["presentation.section.why"]="Neden",["presentation.section.risk"]="Riskler",["presentation.section.opportunity"]="Fırsatlar",["presentation.section.actions"]="Emirler",["presentation.navigation.root"]="Fall of Cavalry · 1648",["presentation.navigation.campaign"]="Sefer Defteri",["presentation.navigation.back"]="Geri",["presentation.navigation.forward"]="İleri",["presentation.clock.unavailable"]="1 Eylül 1648",["presentation.alerts"]="Uyarılar",["presentation.context.title"]="Bağlam",["presentation.context.no-selection"]="Ek bağlam seçilmedi",["presentation.preview.visual-soldier-reuse"]="Asker görünümü seçili birlikten üretilir",["presentation.empty.risks"]="Bilinen kritik risk yok",["presentation.empty.opportunities"]="Kayıtlı fırsat yok",["presentation.empty.actions"]="Uygulanabilir emir yok",
            ["presentation.city.name"]="Şehir",["presentation.city.population"]="Nüfus (slice ayarı)",["presentation.city.areas"]="Şehir alanları",["presentation.city.officials"]="Şehir görevlileri",["presentation.city.market"]="Pazar",["presentation.city.production"]="Üretim",["presentation.city.infrastructure"]="Altyapı",["presentation.city.religion"]="Dinî profil",
            ["presentation.character.name"]="Ad",["presentation.character.importance"]="Önem",["presentation.character.life"]="Yaşam durumu",["presentation.character.location"]="Konum",["presentation.character.loyalty"]="Sadakat",["presentation.character.satisfaction"]="Memnuniyet",["presentation.character.reputation"]="İtibar",["presentation.character.standing"]="Mevki",["presentation.character.injury"]="Yaralanma",["presentation.character.captivity"]="Esaret",["presentation.character.religion"]="Din",["presentation.character.sect"]="Mezhep",["presentation.character.stats"]="Nitelikler",["presentation.character.memberships"]="Üyelikler",["presentation.character.assignments"]="Görevler",["presentation.character.houses"]="Haneler",["presentation.character.cliques"]="Çevreler",["presentation.character.relations"]="İlişkiler",["presentation.character.contracts"]="Sözleşmeler",
            ["presentation.life.alive"]="Hayatta",["presentation.life.dead"]="Ölü",["presentation.injury.light"]="Hafif",["presentation.injury.serious"]="Ciddi",["presentation.injury.permanent"]="Kalıcı",["presentation.injury.unfitforduty"]="Görev yapamaz",["presentation.state.captive"]="Esir",
            ["presentation.trade.cash"]="Kasa",["presentation.trade.demand"]="Talep",["presentation.trade.caravans"]="Kervanlar",["presentation.army.name"]="Ordu",["presentation.army.headcount"]="Mevcut",["presentation.army.commander"]="Komutan",["presentation.army.owner"]="Sahip",["presentation.army.controller"]="Denetleyen",["presentation.army.morale"]="Moral",["presentation.army.fatigue"]="Yorgunluk",["presentation.army.discipline"]="Disiplin",["presentation.army.units"]="Birlikler",["presentation.army.soldiers"]="Kalıcı askerler",["presentation.army.supply"]="Erzak ve mühimmat",["presentation.army.payroll"]="Ulufe ve ödemeler",
            ["presentation.map.slice"]="Tarihsel kesit",["presentation.map.known-markers"]="Bilinen noktalar",["presentation.map.route-graph"]="Yol ağı",["presentation.map.active-journeys"]="Etkin yolculuklar",["presentation.map.selected"]="Seçili yer",["presentation.map.locations"]="Yerler",["presentation.map.routes"]="Yollar",["presentation.map.travel-progress"]="Yolculuk ilerlemesi",
            ["presentation.travel.started"]="Yolculuk başladı",["presentation.travel.advanced"]="Bir saat ilerledi",["presentation.action.validation-rejected"]="İşlem şu anda yapılamıyor",["presentation.knowledge.self"]="Doğrudan bilgi",["presentation.knowledge.unknown"]="Bilinmiyor",["presentation.knowledge.delivered-report"]="Ulaşmış rapor",["presentation.trend.insufficient-history"]="Yeterli geçmiş yok",["presentation.why.not-exposed-by-gameplay"]="Bu bilgi mevcut oynanış yetkisiyle gösterilmiyor",["presentation.reason.unknown-to-viewer"]="Bu bilgi henüz bilinmiyor",["presentation.reason.no-selection"]="Bir kayıt seçilmedi",["presentation.value.none"]="Yok",["presentation.value.unknown"]="Bilinmiyor",["presentation.value.not-applicable"]="Uygulanamaz",["presentation.state.active"]="Etkin",["presentation.state.inactive"]="Etkin değil",["presentation.state.installed"]="Kurulu",["presentation.state.not-installed"]="Kurulu değil",["presentation.state.unassigned"]="Atanmamış",["presentation.state.none"]="Yok",["presentation.precision.exact"]="Kesin",["presentation.precision.approximate"]="Yaklaşık",["presentation.precision.qualitative"]="Nitel",["presentation.precision.unknown"]="Bilinmiyor",
            ["presentation.morale.unassessed"]="Değerlendirilmedi",["presentation.fatigue.unassessed"]="Değerlendirilmedi",["presentation.discipline.unassessed"]="Değerlendirilmedi",
            ["presentation.entity.istanbul"]="İstanbul",["presentation.entity.bursa"]="Bursa",["presentation.entity.edirne"]="Edirne",["presentation.entity.izmit"]="İzmit",["presentation.entity.city-istanbul"]="İstanbul",["presentation.entity.city-bursa"]="Bursa",["presentation.entity.city-edirne"]="Edirne",["presentation.entity.city-izmit"]="İzmit",["presentation.entity.hasan-aga"]="Hasan Ağa",["presentation.entity.sultan-mehmed-iv"]="Sultan IV. Mehmed",["presentation.entity.kosem-sultan"]="Kösem Sultan",["presentation.entity.sofu-mehmed-pasa"]="Sofu Mehmed Paşa",["presentation.entity.army-hasan-retinue"]="Hasan Ağa'nın Kapı Halkı",["presentation.entity.unit-hasan-sipahi"]="Timarlı Sipahiler",["presentation.entity.unit-hasan-cebeli"]="Cebeliler",["presentation.entity.unit-hasan-tufekci"]="Tüfekli Piyadeler"
        };
    }

    /// <summary>Never exposes an internal presentation.* key in the playable historical slice.</summary>
    public sealed class SlicePresentationLocalizer : IPresentationLocalizer
    {
        public string Get(string key)
        {
            if(string.IsNullOrWhiteSpace(key))return string.Empty;if(SliceLocalization.Turkish.TryGetValue(key,out var translated))return translated;if(!key.StartsWith("presentation.",StringComparison.Ordinal))return key;
            if(key.StartsWith("presentation.action.travel-to.",StringComparison.Ordinal))return key.Substring("presentation.action.travel-to.".Length)+" yönüne yola çık";
            var segment=key.Substring(key.LastIndexOf('.')+1).Replace('-',' ');return CultureInfo.GetCultureInfo("tr-TR").TextInfo.ToTitleCase(segment);
        }
    }
}
