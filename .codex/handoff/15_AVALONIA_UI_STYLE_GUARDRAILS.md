# Avalonia UI Style Guardrails — Proxima

## Goal

Prevent visual drift from Figma and prevent default Avalonia/Fluent styles from leaking into Proxima UI.

## Non-negotiable rules

### 1. No raw visual values in page XAML

Forbidden in `src/Proxima.App/Views/**/*.axaml`:

- raw hex colors, e.g. `#FFFFFF`, `#001629`;
- inline `Background`;
- inline `Foreground`;
- inline `BorderBrush`;
- inline `CornerRadius`;
- inline `FontSize`;
- inline `FontWeight`;
- inline `Padding` for reusable components;
- inline `BoxShadow`;
- page-local reusable styles.

Allowed only inside design-system/theme files:

```text
src/Proxima.App/DesignSystem/**/*.axaml
src/Proxima.App/Styles/**/*.axaml
```

### 2. No default Avalonia appearance in production screens

Every visual control in a screen must use one of:

- a Proxima wrapper component;
- a Proxima `ControlTheme`;
- a Proxima style class approved by the design system.

Forbidden in production views:

```xml
<Button Content="Создать портфель" />
<TextBox Watermark="Поиск" />
<ComboBox ItemsSource="{Binding Portfolios}" />
```

Required pattern:

```xml
<Button Theme="{DynamicResource ProximaButtonPrimaryTheme}"
        Content="Создать портфель" />

<TextBox Theme="{DynamicResource ProximaTextBoxSearchTheme}"
         Watermark="Поиск" />

<ComboBox Theme="{DynamicResource ProximaComboBoxTheme}"
          ItemsSource="{Binding Portfolios}" />
```

### 3. No reusable visual styling in page views

Do not define reusable visual styles in `Views/**/*.axaml`.

Forbidden:

```xml
<UserControl.Styles>
  <Style Selector="Button">
    ...
  </Style>
</UserControl.Styles>
```

### 4. Figma values must map to tokens

If Figma contains a value that does not exist in Proxima tokens, do not approximate it inline.

Update:

```text
.codex/design-token-gaps.md
```

Format:

```md
| Figma Node | Value | Proposed Token | Usage | Status |
| --- | --- | --- | --- | --- |
```

### 5. UI acceptance requires visual evidence

A screen is not accepted until:

- the Figma source node is recorded;
- component mapping exists;
- token mapping exists;
- visual comparison is recorded;
- style guard passes;
- no default Avalonia visual leakage is visible;
- no text clipping or overflow is present at target window sizes.

## Required UI guard command

```bash
bash scripts/scan-xaml-style-violations.sh src/Proxima.App/Views
```

## Temporary exception

`src/Proxima.App/MainWindow.axaml` is legacy UI during recovery. New or refactored screens must live under `src/Proxima.App/Views` and must pass this guard. Do not continue expanding `MainWindow.axaml`.
