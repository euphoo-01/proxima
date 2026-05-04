# Figma to Avalonia Mapping Contract

## Rule

Never implement a Figma screen directly into page XAML.

Before implementation, create a mapping file:

```text
.codex/figma-mapping/<screen-name>.md
```

## Required mapping template

```md
# <Screen> Figma Mapping

## Source

- Figma file key: `Drxcen3JN69XP0fnYxkgOi`
- Figma node:
- Screen name:
- Date inspected:
- MCP provider/tool:

## Frame metrics

| Property | Figma value | Avalonia decision |
| --- | --- | --- |
| Width | | |
| Height | | |
| Main padding | | |
| Grid gap | | |
| Card radius | | |
| Header height | | |

## Layout mapping

| Figma layer | Avalonia container/component | Notes |
| --- | --- | --- |

## Component mapping

| Figma component/layer | Avalonia component | Status | Notes |
| --- | --- | --- | --- |

## Token mapping

| Figma value | Proxima token | Status |
| --- | --- | --- |

## States

| State | Figma source | Avalonia implementation |
| --- | --- | --- |
| Loading | | |
| Empty | | |
| Error | | |
| Hover/focus | | |

## Visual comparison result

| Check | Pass/Fail | Notes |
| --- | --- | --- |
| Composition | | |
| Spacing rhythm | | |
| Typography | | |
| Colors | | |
| Icons | | |
| No clipping | | |
| No default Avalonia leakage | | |

## Known deviations

| Deviation | Reason | Severity | Follow-up |
| --- | --- | --- | --- |
```

## Required core mappings

Create mapping files for:

- `login.md` — node `62:2149`;
- `register.md` — node `62:2417`;
- `dashboard.md` — node `62:1882`;
- `assets.md` — node `62:1195`;
- `asset-details.md` — node `62:1521`;
- `goals.md` — node `62:962`;
- `taxes.md` — node `62:498`;
- `settings.md` — node `62:763`.

## Prohibited

- Starting implementation before mapping exists.
- Guessing layout from memory.
- Reusing wireframe when a Mockup node exists.
- Duplicating the same visual component in multiple pages.
- Creating a new token inline instead of recording token gap first.
