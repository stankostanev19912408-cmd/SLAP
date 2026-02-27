# Сборка APK (Unity 2022.3.62f3)

## Вариант 1: из Unity Editor (рекомендуется)
1. Открой проект `SLAP` в Unity.
2. Проверь `File -> Build Settings`:
   - Платформа: `Android` (должна быть активной).
   - В сценах включена `Assets/Scenes/SampleScene.unity`.
3. Запусти автофикс сцены:
   - `Tools -> Slap -> Repair Build Scene Combat Setup`.
4. Нажми `Build` (или `Build And Run`).
5. Сохрани файл как `slap.apk`.

## Вариант 2: batch build (CLI)
Команда:

```powershell
S:\unity\2022.3.62f3\Editor\Unity.exe `
  -batchmode -quit `
  -projectPath "s:\Projects\Slap_Prototype\SLAP" `
  -executeMethod SlapBatchBuild.BuildAndroidApk `
  -logFile "Reports\unity_build.log"
```

APK будет в корне проекта:
- `s:\Projects\Slap_Prototype\SLAP\slap.apk`

## Если сборка падает из-за лицензии
Признак в логе: `return code 199` / проблемы `LicensingClient`.

Что делать:
1. Закрой все процессы Unity.
2. Запусти `Unity Hub` и убедись, что лицензия активна.
3. Один раз открой проект в Editor, дождись полной загрузки.
4. Повтори batch build.

## Быстрая проверка результата
1. Удали старую версию приложения с телефона.
2. Установи новый `slap.apk`.
3. На старте должны быть:
   - экран выбора сложности и кнопка `SLAP`,
   - стартовая анимация разминки,
   - анимации реакции при пропущенных ударах.
