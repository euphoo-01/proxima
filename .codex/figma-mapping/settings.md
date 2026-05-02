# Settings Figma Mapping

## 1) Figma node id
- Root: `62:763`
- Main content: `62:2792`

## 2) Список экранных областей
- Sidebar area
- Topbar/header area
- Profile/account settings section
- Security settings section
- Integrations/preferences section

## 3) Figma layer → Avalonia component
| Figma layer | Avalonia component |
| --- | --- |
| `62:763 Settings` | `Settings/SettingsView` |
| `62:2793 Sidebar` | `Shell/SidebarView` |
| `62:2362 Header` | `Shell/TopbarView` + `PageHeader` |
| `62:764 Main Canvas` | grouped bento setting cards |

## 4) Figma token/value → Proxima token
| Figma token/value | Proxima token |
| --- | --- |
| Page bg `#F8FAFB` | `ProximaBrush.Page` |
| Surface `#FFFFFF` | `ProximaBrush.Surface` |
| Border subtle `#EDEEF0` | `ProximaBrush.BorderSubtle` |
| Focus `#1D6FD6` | `ProximaBrush.Focus` |
| Radius `12/16` | `ProximaRadius.Card` / `ProximaRadius.Large` |
| Spacing `16/24/32` | `ProximaSpace.16` / `.24` / `.32` |

## 5) Список недостающих токенов
- Нет обязательных новых токенов.

## 6) Список недостающих компонентов
- `SettingsSectionCard`
- `SettingsToggleRow`
- `SettingsDangerAction`

## 7) Список допустимых визуальных отклонений
- Допускается упрощение разделителей внутри карточек до стандартного `BorderBrush` DS.
