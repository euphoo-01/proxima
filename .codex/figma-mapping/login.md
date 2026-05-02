# Login Figma Mapping

## 1) Figma node id
- Root: `62:2149`
- Card/Form: `62:2165`

## 2) Список экранных областей
- Page background container
- Auth card container
- Header block
- Input/action form block

## 3) Figma layer → Avalonia component
| Figma layer | Avalonia component |
| --- | --- |
| `62:2149 Login` | `Auth/LoginView` root container |
| `62:2165 Form` | `AuthCard` (new reusable control) |
| `62:2166 Header` | `TextBlock` + subtitle block in `AuthCard` |
| `62:2172 Form` | `StackPanel` with `TextBox`, `PasswordBox`, `Button` (Proxima themes) |

## 4) Figma token/value → Proxima token
| Figma token/value | Proxima token |
| --- | --- |
| Background `#F8FAFB` | `ProximaBrush.Page` |
| Card fill `#FFFFFF` | `ProximaBrush.Surface` |
| Card radius `24` | `ProximaRadius.AuthCard` (gap) |
| Card shadow `0 20 40 rgba(0,29,52,0.06)` | `ProximaShadow.AuthCard` (gap) |
| Horizontal padding `24` | `ProximaSpace.24` |
| Card inner padding `48` | `ProximaSpace.48` (gap) |
| Brand action color `#0F4C81` | `ProximaBrush.Brand` |
| Border subtle `#E2E8F0/#EDEEF0` | `ProximaBrush.Border` / `ProximaBrush.BorderSubtle` |

## 5) Список недостающих токенов
- `ProximaRadius.AuthCard = 24`
- `ProximaSpace.48 = 48`
- `ProximaShadow.AuthCard = 0 20 40 0 #0F001D34(6%)`

## 6) Список недостающих компонентов
- `AuthCard`
- `AuthFormFieldGroup` (label + control + validation)

## 7) Список допустимых визуальных отклонений
- Вертикальное центрирование формы допускается с адаптивным `MinHeight`, без фиксирования абсолютного `Y` из Figma.
- Текст кнопок/лейблов может иметь +/−1 px line-height из-за Avalonia text rendering.
