# Roadmap — Forza Horizon 6 Tune Master

*Last updated: 2026-07-21* | *Основано на обратной связи пользователей*

---

## Приоритет 1 — Критические баги

### 17. Ручная коробка передач не использует весь диапазон оборотов
**Отзыв пользователя:** «Gearing works well, but it doesn't accommodate the entire rev range when using manual.» Автоматическая коробка ощущается нормально, но при ручной передаточные числа не растягиваются так, чтобы каждая передача доходила до редлайна/использовала весь рабочий диапазон RPM.

**Что сделать:**
- [ ] Проверить `GearingCalculator`/`BuildDisciplineRatios` на предмет разницы поведения auto vs manual (флаг коробки может обрезать растяжку передач)
- [ ] Сверить с недавним фиксом сжатия gear-ladder (`c3de745`) — не регрессия ли это того же места
- [ ] Тест на нескольких машинах/дисциплинах: ручная коробка должна использовать полный диапазон RPM на каждой передаче, а не только на части

### 19. Драг-тюн «упирается» в заданную пользователем отметку максималки вместо реальной оптимизации
**Отзыв пользователя:** «your drag settings are... eh at best, its trying to top out at the mark you've set.» То есть расчёт передач на драге ощущается так, будто просто подгоняется под целевую скорость/дистанцию, а не считает реально оптимальный разгон.

**Что сделать:**
- [ ] Перепроверить после фикса `c3de745` (trap-speed cap, Mile distance factor) — воспроизводится ли ещё это ощущение
- [ ] Если да — разобраться, не зажимает ли `GearingCalculator` передачи искусственно под целевой trap speed вместо честной оптимизации ускорения
- [ ] Тест: сравнить расчётный разгон/трап-спид с реальными заездами на паре машин

---

## Приоритет 3 — Погрешности в БД

### 6. Вес машин — иногда больше, иногда меньше реального
**Проблема:** `Data_Car.CurbWeight × 100` даёт погрешность ±10 кг; часть машин имеет неверный вес в БД. Минорная проблема, на результат тюна влияет слабо.

**Что сделать:**
- [ ] Провести выборочную сверку CurbWeight по топ-50 машинам с реальными данными
- [ ] Добавить корректирующий слой (hardcoded override list) для явно ошибочных значений
- [ ] Логировать расхождения при первой загрузке

---

## Приоритет 4 — UX / Удобство ввода

### 8. Выбор старого/нового способа ввода данных
**Проблема:** Исчезла возможность быстрого ввода; нужно перебирать десятки дропдаунов, на тюненой машине — вспоминать что уже поставлено.

**Что сделать:**
- [ ] Добавить переключатель «Режим ввода»: **«Быстрый»** (прежние слайдеры/поля ввода) / **«Детальный»** (нынешние дропдауны из БД)
- [ ] В «Быстром» режиме: power, torque, mass, RPM, ride height, tire pressure — ручной ввод; остальное — авто
- [ ] Сохранять выбранный режим в профиль
- [ ] Оба режима должны использовать одни и те же калькуляторы (промежуточные значения в «быстром» — из БД по умолчанию)

### 18. Ручной ввод min/max для демпферов (отбой/сжатие)
**Отзыв пользователя:** «I also find the springs and damping to be average, is it possible to input minimums and maximums to get it more accurate?» Пружины и клиренс уже сделаны; демпферы (rebound/bump) — ещё нет.

**Что сделать:**
- [ ] Добавить в `SelectedParts` override-поля min/max для rebound/bump (перед/зад), по аналогии с уже сделанными `SpringFrontMinOverride`/`SpringFrontMaxOverride`
- [ ] `SuspensionCalculator.CalculateDampers` должен клэмпить в эти границы, если заданы, вместо диапазона из `List_SpringDamperPhysics`
- [ ] UI в `TuneResultView.xaml` — та же пара полей Min/Max с кнопкой сброса, что уже есть у пружин

---

## Приоритет 6 — Новые модули

### 20. Система классов PI (Performance Index)
**Отзыв пользователя:** «Any chance you can add the PI class system to the program as well? Just so that way you can actually build cars into a certain class at the same time. Would be great for figuring out if a car is viable before buying the car itself.» То есть нужен расчёт итогового PI-класса (D/C/B/A/S1/S2/X) собранной сборки, чтобы можно было прикинуть жизнеспособность машины под конкретный класс ДО её покупки.

**Что сделать:**
- [ ] Найти/вывести формулу PI (обычно: взвешенная сумма нормализованных Speed/Handling/Acceleration/Launch/Braking, где нормализация — против диапазона по всей игре) — проверить, нет ли готовых компонентов уже в `Data_Car`/связанных таблицах slim DB
- [ ] Посчитать итоговый PI и класс для собранного `TuneResult` (используя уже посчитанные PowerHP/TorqueNm/Mass/aero/handling метрики — многое уже считается в существующих калькуляторах)
- [ ] Показать PI-класс и число в `TuneResultView`
- [ ] Режим «Build to class»: пользователь выбирает целевой класс (например A800) — приложение подсказывает/ограничивает апгрейды так, чтобы не выйти за его пределы (или показывает «за» / «под» с запасом)
- [ ] Учесть, что PI пересчитывается на каждое изменение `SelectedParts` — переиспользовать существующий пайплайн `PartsChanged`, не городить отдельный
- [ ] Провалидировать точность посчитанного PI на наборе машин с известным в игре классом (см. `feedback_general_fixes` — фиксы/формулы общие для всех машин, не хардкод по одной)

---

## Технический долг (не вошедшее в этот список)

- [ ] CarCard — вычистить legacy enum-поля (TireType, SuspensionUpgrade...)

---

## Roadmap — English Summary

| # | Item | Priority |
|---|------|----------|
| 17 | Manual transmission doesn't use full rev range per gear | P1 — Bug |
| 19 | Drag gearing feels like it just caps out at the user-set target speed instead of truly optimizing | P1 — Bug |
| 6 | Car weight inaccuracies in DB (CurbWeight × 100) | P3 — DB |
| 8 | Old/new data entry mode toggle (sliders vs dropdowns) | P4 — UX |
| 18 | Manual min/max override for dampers (rebound/bump) | P4 — UX |
| 20 | PI (Performance Index) class system — build/check viability before buying a car | P6 — New feature |
