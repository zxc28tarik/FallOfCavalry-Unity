# UI Toolkit design system

Environment: Unity `6000.3.16f1`, built-in `com.unity.modules.uielements` `1.0.0`. UI Toolkit is the sole 2D runtime UI in this package; there is no uGUI exception.

## Assets

- `PresentationTokens.uss`: palette, spacing and control-size tokens.
- `PresentationBase.uss`: shell, typography and primary regions.
- `PresentationComponents.uss`: cards, rows, chips, actions, attention and foldouts.
- `PresentationScreens.uss`: responsive desktop classes and ListView policy.
- `PresentationShell.uxml`: componentized shell structure with named composition points.

The visual language uses charcoal surfaces, restrained brass emphasis and semantic warning/success/unknown colors. It is repository-authored and deliberately avoids copying a commercial strategy-game interface. No external art or font asset is included.

All large data surfaces use `ListView` with fixed-height recycling. Labels, status text and tooltips carry localization keys. Focusable controls, Escape/back, scroll and pointer input are supported. UI scale is a runtime layout input (`0.75–1.5`) and is not campaign save data.

The current batch environment is `-nographics`; therefore structural tests are authoritative for this package and no rendered screenshot is claimed.

