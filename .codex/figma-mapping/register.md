# Register Figma Mapping

## 1) Figma node id
- Root: `62:2417`
- Card/Form: `62:2418`

## 2) Список экранных областей
- Page background container
- Registration card container
- Header block
- Registration fields/actions block

## 3) Figma layer → Avalonia component
| Figma layer | Avalonia component |
| --- | --- |
| `62:2417 Register` | `Auth/RegisterView` root container |
| `62:2418 Form` | `AuthCard` (shared with Login) |
| `62:2419 Header` | `TextBlock` header/subtitle |
| `62:2425 Form` | `StackPanel` form controls with Proxima themes |

## 4) Figma token/value → Proxima token
| Figma token/value | Proxima token |
| --- | --- |
| Background `#F8FAFB` | `ProximaBrush.Page` |
| Card fill `#FFFFFF` | `ProximaBrush.Surface` |
| Card radius `24` | `ProximaRadius.AuthCard` (gap) |
| Shadow `0 20 40 rgba(0,29,52,0.06)` | `ProximaShadow.AuthCard` (gap) |
| Outer horizontal padding `24` | `ProximaSpace.24` |
| Card padding `48` | `ProximaSpace.48` (gap) |

## 5) Список недостающих токенов
- `ProximaRadius.AuthCard = 24`
- `ProximaSpace.48 = 48`
- `ProximaShadow.AuthCard = 0 20 40 0 #0F001D34(6%)`

## 6) Список недостающих компонентов
- `AuthCard` (shared)
- `PasswordStrengthHint` block (if represented in Figma form states)

## 7) Список допустимых визуальных отклонений
- Допускается динамический рост высоты формы по валидационным сообщениям.
- Допускается перенос вторичного текста на 2 строки при локализации.
