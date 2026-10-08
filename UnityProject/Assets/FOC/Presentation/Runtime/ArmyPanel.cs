#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using FOC.Application.Military;
using FOC.Presentation.Core;
using UnityEngine.UIElements;

namespace FOC.Presentation.Unity
{
    internal sealed class ArmyPanel : IDisposable
    {
        private readonly VisualElement _root;
        private readonly ArmyPanelSession _session;
        private readonly Action<string> _navigate;
        private readonly Action<PresentationActionResult> _completed;
        private readonly DropdownField _army = new DropdownField("Ordu") { name = "army-selector" },
            _kind = new DropdownField("İşlem") { name = "army-kind", choices = new List<string> { "Asker toplama", "Şehir ikmali", "Kervan ikmali", "Maaş ödemesi" } },
            _source = new DropdownField("Kaynak") { name = "army-source" },
            _detail = new DropdownField("Tür / mal") { name = "army-detail" };
        private readonly TextField _quantity = new TextField("Miktar") { name = "army-quantity", value = "1", maxLength = 20 };
        private readonly Label _summary = new Label { name = "army-summary" }, _resources = new Label { name = "army-resources" },
            _reason = new Label { name = "army-reason" }, _note = new Label { name = "army-note" },
            _confirmation = new Label { name = "army-confirmation" }, _feedback = new Label { name = "army-feedback" };
        private readonly Button _prepare, _confirm, _cancel;
        private ArmyPanelSnapshot? _snapshot;
        private bool _disposed;
        public ArmyPanel(VisualElement root, ArmyPanelSession session, Action<string> navigate, Action<PresentationActionResult> completed)
        {
            _root = root; _session = session; _navigate = navigate; _completed = completed;
            var title = new Label("ORDU · PERSONEL VE İKMAL"); title.AddToClassList("foc-card-title"); root.Add(title);
            root.Add(_summary); root.Add(_resources);
            var selectors = new VisualElement(); selectors.AddToClassList("foc-trade-selectors"); selectors.Add(_army); selectors.Add(_kind); root.Add(selectors);
            root.Add(_source);
            var fields = new VisualElement(); fields.AddToClassList("foc-trade-selectors"); fields.Add(_detail); fields.Add(_quantity); root.Add(fields);
            root.Add(_note); root.Add(_reason);
            var row = new VisualElement(); row.AddToClassList("foc-trade-buttons");
            _prepare = new Button(Prepare) { name = "army-prepare", text = "İşlemi incele" };
            _confirm = new Button(Confirm) { name = "army-confirm", text = "Onayla" };
            _cancel = new Button(Cancel) { name = "army-cancel", text = "Vazgeç" };
            root.Add(_confirmation); row.Add(_prepare); row.Add(_confirm); row.Add(_cancel); root.Add(row); root.Add(_feedback);
            _army.RegisterValueChangedCallback(OnArmy); _kind.RegisterValueChangedCallback(OnKind);
            _source.RegisterValueChangedCallback(OnSelection); _detail.RegisterValueChangedCallback(OnSelection); _quantity.RegisterValueChangedCallback(OnSelection);
        }
        public void Render(string? army = null)
        {
            if (_disposed) return;
            _session.OpenArmy(army); var s = _session.Read(); _snapshot = s;
            Choices(_army, s.Armies, s.ArmyId); Choices(_source, s.Sources, s.SourceId); Choices(_detail, s.Details, s.DetailId);
            _kind.SetValueWithoutNotify(_kind.choices[(int)s.Kind]); _quantity.SetValueWithoutNotify(s.Quantity);
            _detail.style.display = s.Kind == ArmyOrderKind.Payroll ? DisplayStyle.None : DisplayStyle.Flex;
            _summary.text = s.Summary; _resources.text = s.Resources; _note.text = s.Note;
            _reason.text = ArmyPanelSession.Reason(s.Failure); _feedback.text = s.Feedback;
            _prepare.SetEnabled(s.Failure == ArmyOrderFailure.None && s.Confirmation == null);
            _confirm.style.display = _cancel.style.display = s.Confirmation == null ? DisplayStyle.None : DisplayStyle.Flex;
            _confirm.SetEnabled(s.Confirmation != null); _cancel.SetEnabled(s.Confirmation != null);
            _confirmation.text = s.Confirmation == null ? "" : "ONAY · " + _kind.value + " · " + _source.value + "\n"
                + (s.Kind == ArmyOrderKind.Payroll ? "Ödeme" : _detail.value) + " × " + s.Confirmation.Quantity + " → " + _army.value;
        }
        private static void Choices(DropdownField field, IReadOnlyList<ArmyPanelChoice> choices, string id)
        { field.choices = choices.Select(x => x.Label).ToList(); field.SetValueWithoutNotify(choices.FirstOrDefault(x => x.Id == id)?.Label ?? "—"); field.SetEnabled(choices.Count > 0); }
        private static string Id(DropdownField field, IReadOnlyList<ArmyPanelChoice> choices) => choices.ElementAtOrDefault(field.choices.IndexOf(field.value))?.Id ?? "";
        private void OnArmy(ChangeEvent<string> evt) { if (!_disposed && _snapshot != null) { var id = Id(_army, _snapshot.Armies); if (id.Length > 0) _navigate(id); } }
        private void OnKind(ChangeEvent<string> evt)
        { if (!_disposed && _snapshot != null) { _session.Select(_snapshot.ArmyId, (ArmyOrderKind)_kind.choices.IndexOf(_kind.value), "", "", _quantity.value); Render(); } }
        private void OnSelection(ChangeEvent<string> evt)
        { if (!_disposed && _snapshot != null) { _session.Select(_snapshot.ArmyId, _snapshot.Kind, Id(_source, _snapshot.Sources), Id(_detail, _snapshot.Details), _quantity.value); Render(); } }
        private void Prepare() { if (!_disposed) { _session.Prepare(); Render(); } }
        private void Confirm() { if (!_disposed) { var result = _session.Confirm(); _completed(result); Render(); } }
        private void Cancel() { if (!_disposed) { _session.Cancel(); Render(); } }
        public void Dispose()
        {
            if (_disposed) return; _disposed = true; _session.Cancel();
            _army.UnregisterValueChangedCallback(OnArmy); _kind.UnregisterValueChangedCallback(OnKind);
            _source.UnregisterValueChangedCallback(OnSelection); _detail.UnregisterValueChangedCallback(OnSelection); _quantity.UnregisterValueChangedCallback(OnSelection);
            _prepare.clicked -= Prepare; _confirm.clicked -= Confirm; _cancel.clicked -= Cancel;
            _snapshot = null; _root.Clear();
        }
    }
}
