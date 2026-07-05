# Roadmap — Forza Horizon 6 Tune Master

*Last updated: 2026-06-29* | *Основано на обратной связи пользователей*

---

## Приоритет 1 — Критические баги

### 1. Давление в шинах — не рассчитывается, всегда stock
**Проблема:** TireCalculator перестал вычислять давление; для любого типа покрышек отдаётся только `DefaultPressureFront/Rear` из `List_UpgradeTireCompound`, без поправок на массу, сезон, дисциплину.

**Что сделать:**
- [ ] Восстановить расчёт давления с учётом массы авто, сезона и дисциплины, используя `DefaultPressure` как базу + коэффициенты

### 2. Отсутствуют drift-покрышки в списке
**Проблема:** `GetTireCompounds(ordinal)` не возвращает drift-покрышки (или они фильтруются).

**Что сделать:**
- [ ] Проверить таблицу `List_UpgradeTireCompound` — есть ли записи для drift
- [ ] Если фильтр в коде их отсекает — убрать; если в БД нет — добавить fallback/хотя бы пометить

### 3. Нет скроллбаров в поле результатов
**Проблема:** При увеличении шрифта часть результатов TuneResultView уходит за границы и невидима — скроллбаров нет.

**Что сделать:**
- [ ] Добавить `ScrollViewer` / `VirtualizingStackPanel` в TuneResultView
- [ ] Проверить, что скролл работает при любом масштабе шрифта

---

## Приоритет 2 — UI / Масштабирование

### 4. Масштабирование под 4K мониторы
**Проблема:** На 4K всё нечитаемо, на 1080p — на грани. Нет адаптивного масштабирования.

**Что сделать:**
- [ ] Внедрить `Viewbox` или ScaleTransform в MainWindow для автоматического масштабирования под DPI
- [ ] Проверить в `app.manifest` — `dpiAware` / `dpiAwareness` = `PerMonitorV2`
- [ ] Добавить ручную настройку масштаба (слайдер / ComboBox: 100%, 125%, 150%, 175%, 200%)
- [ ] Проверить все UserControl'ы на читаемость в 4K

### 5. Размер шрифта в результатах
**Проблема:** Можно крутить шрифт, но нет слайдера/контрола — только через системные настройки.

**Что сделать:**
- [ ] Добавить контрол масштаба шрифта в UI (например, в статус-бар или меню)
- [ ] Синхронизировать с общим масштабом из п.4

---

## Приоритет 3 — Погрешности в БД

### 6. Вес машин — иногда больше, иногда меньше реального
**Проблема:** `Data_Car.CurbWeight × 100` даёт погрешность ±10 кг; часть машин имеет неверный вес в БД. Минорная проблема, на результат тюна влияет слабо.

**Что сделать:**
- [ ] Провести выборочную сверку CurbWeight по топ-50 машинам с реальными данными
- [ ] Добавить корректирующий слой (hardcoded override list) для явно ошибочных значений
- [ ] Логировать расхождения при первой загрузке

### 7. Размеры колёс (профиль/диаметр) — некорректны для некоторых машин
**Проблема:** Audi Quattro — профиль больше реального. Данные из БД (`FrontTireWidthMM`, `FrontTireAspect`, `FrontWheelDiameterIN`) иногда ошибочны.

**Что сделать:**
- [ ] Проверить таблицу `Data_Car` на предмет аномальных значений (Aspect < 20 или > 80, Diameter < 13 или > 22)
- [ ] Сверить audi_quattro (CarId уточнить) с реальными данными
- [ ] Добавить override list для колёс

### 14. Профиль шин не меняется при смене ширины покрышек
**Проблема:** На Audi Quattro при выборе любого уровня `TireWidth` профиль (`Aspect`) остаётся стоковым, вместо того чтобы пересчитываться под новую ширину. Выбор ширины покрышек не влияет на профиль.

**Что сделано:**
- [x] Установлено: в БД для Audi Quattro нет апгрейдов профиля (только Offset=0)
- [x] `UpdateTireAndWheelData()` теперь auto-рассчитывает профиль при смене ширины: `newProfile = round(stockWidth × stockProfile / newWidth)`, сохраняя высоту боковины
- [x] Если пользователь явно выбрал профиль через ComboBox — его выбор приоритетен
- [x] Добавлены `StockFrontTireWidth` / `StockRearTireWidth` в CarCard
- [x] Тест: Audi Quattro 235/45 → 275/38 (sidewall сохранён)

### 15. Расчёт жёсткости пружин превышает физические лимиты детали
**Проблема:** На Audi Quattro с раллийной подвеской задние пружины — 187 кгс/мм, при максимуме детали ≈136. SuspensionCalculator выдаёт значения за границами возможного для выбранного SpringDamper.

**Что сделать:**
- [ ] Добавить clamping результата SpringCalculator в `[DefSpringRate − delta, DefSpringRate + delta]` из `List_SpringDamperPhysics`
- [ ] Проверить все калькуляторы подвески на предмет выхода за min/max детали
- [ ] Добавить тест: rally suspension + Audi Quattro → spring rate в допустимых пределах

---

## Приоритет 4 — UX / Удобство ввода

### 8. Выбор старого/нового способа ввода данных
**Проблема:** Исчезла возможность быстрого ввода; нужно перебирать десятки дропдаунов, на тюненой машине — вспоминать что уже поставлено.

**Что сделать:**
- [ ] Добавить переключатель «Режим ввода»: **«Быстрый»** (прежние слайдеры/поля ввода) / **«Детальный»** (нынешние дропдауны из БД)
- [ ] В «Быстром» режиме: power, torque, mass, RPM, ride height, tire pressure — ручной ввод; остальное — авто
- [ ] Сохранять выбранный режим в профиль
- [ ] Оба режима должны использовать одни и те же калькуляторы (промежуточные значения в «быстром» — из БД по умолчанию)

### 9. Ручной ввод RPM-лимитов
**Проблема:** RPM limits берутся из camshaft (автомат), при ручном переключении — другие.

**Что сделать:**
- [ ] Добавить поле «Max RPM Override» — nullable, по умолчанию null (авто из camshaft)
- [ ] При ручном вводе — использовать в PowerCalculator и GearingCalculator вместо camshaft.RedlineRPM
- [ ] Показывать предупреждение если ввод >> разумного для двигателя

### 10. Ручной ввод клиренса (ride height)
**Проблема:** Клиренс — визуальная настройка; текущий авто-расчёт не даёт гибкости.

**Что сделать:**
- [ ] Добавить поля «Front Ride Height Override» / «Rear Ride Height Override» — nullable
- [ ] При вводе — использовать вместо `ModelFrontRideHeightM` из БД
- [ ] Показывать стоковый клиренс и мин/макс для справки

---

## Приоритет 5 — QoL

### 11. Быстрый сброс деталей в stock
**Проблема:** На тюненой машине — долго возвращать всё в сток.

**Что сделано:**
- [x] Кнопка «Сбросить всё в Stock» — в нижней панели (Profiles bar)
- [x] Кнопки «✕» в заголовке каждой категории (Swaps, Engine, Suspension, Transmission, Wheels, Motor)
- [x] Разметка свойств атрибутом `[ResetToStock("Category")]` + метод `ResetCategory()`

### 12. Индикатор изменённых деталей
**Проблема:** Непонятно, какие детали уже заменены на не-stock.

**Что сделать:**
- [ ] В ComboBox: жирный шрифт / цвет для выбранной не-stock детали
- [ ] В заголовке категории: счётчик «3/13 изменено»

### 13. Поиск по деталям
- [ ] Фильтр в ComboBox (IsEditable + StartsWith), чтобы не скроллить 50 позиций

---

## Технический долг (не вошедшее в этот список)

- [ ] `TuningConstraints.cs` — удалить (уже не используется)
- [ ] `UnitConverter.cs` — удалить (уже не используется)
- [ ] CarCard — вычистить legacy enum-поля (TireType, SuspensionUpgrade...)
- [ ] Стабилизировать тесты (14 pre-existing failures)

---

## Roadmap — English Summary

| # | Item | Priority |
|---|------|----------|
| 1 | Tire pressure calculation broken — always returns stock defaults | P1 — Bug |
| 2 | Drift tires missing from compound list | P1 — Bug |
| 3 | No scrollbars in TuneResultView — results invisible when font is large | P1 — Bug |
| 4 | 4K UI scaling — unreadable on high-DPI | P2 — UI |
| 5 | Font size control (slider/selector) | P2 — UI |
| 6 | Car weight inaccuracies in DB (CurbWeight × 100) | P3 — DB |
| 7 | Wheel size mismatches (profile/diameter) for some cars | P3 — DB |
| 14 | Tire profile doesn't update when tire width is changed | P3 — DB |
| 15 | Spring rate calculation exceeds part limits — clamping added | P3 — Calc | ✅ |
| 8 | Old/new data entry mode toggle (sliders vs dropdowns) | P4 — UX |
| 9 | Manual RPM limit override | P4 — UX |
| 10 | Manual ride height override | P4 — UX |
| 11 | "Reset all to Stock" button + per-category reset | P5 — QoL | ✅ |
| 12 | Changed-parts indicator (bold/color in dropdowns) | P5 — QoL |
| 13 | Search/filter in part ComboBoxes | P5 — QoL |
