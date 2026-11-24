# MotorcycleAI

MotorcycleAI היא אפליקציית Web בעברית שמסייעת למצוא אופנועים לפי צרכי המשתמש. היא מנתחת בקשות בשפה טבעית ומחזירה המלצות מתוך מאגר האופנועים. בחיפוש, מודל השפה מפעיל את הכלי `SearchMotorcycles`; הקוד מאמת את הפרמטרים ומריץ חיפוש מוגבל מול מסד הנתונים.

## רכיבי הפרויקט

- **MotorcycleAI.API** – API ואפליקציית Web סטטית, זמינים כברירת מחדל בכתובת `http://localhost:5150`.
- **MotorcycleAI.Core** – מודלים, ממשקים ולוגיקת ניתוח והמלצות.
- **MotorcycleAI.Infrastructure** – גישה ל-SQL Server וחיבור לשרת llama.cpp.
- **MotorcycleAI.Tests** – בדיקות יחידה ואינטגרציה.

## מה צריך להתקין

1. **.NET 10 SDK** – נדרש לבנייה ולהרצה של הפרויקט.
2. **SQL Server Express** – יש ליצור או להתקין מופע בשם `SQLEXPRESS`.
3. **SQL Server Command Line Utilities (`sqlcmd`) או SQL Server Management Studio (SSMS)** – להרצת סקריפטי יצירת מסד הנתונים.
4. **llama.cpp ל-Windows** – יש להוריד ולחלץ גרסה שכוללת `llama-server.exe` (הוראות בהמשך).
5. **מודל GGUF** שתואם ל-llama.cpp ותומך ב-function/tool calling, כולל chat template מתאים. לדוגמה, Qwen3 Instruct; מודל שלא מחזיר tool calls לא יוכל לבצע חיפוש.

הכלי היחיד שחשוף למודל הוא `SearchMotorcycles`, שמקבל מסנני חיפוש ומחזיר אופנועים מהמאגר. המודל אינו מקבל גישה ישירה למסד הנתונים ואינו כותב SQL. שאילתת SQL נבנית בקוד ומשתמשת בפרמטרים.

אין צורך להתקין Node.js: ממשק המשתמש משתמש ב-JavaScript רגיל ומוגש מתוך `MotorcycleAI.API\wwwroot`.

## הכנה ראשונית

### 1. שחזור חבילות .NET

פתחו PowerShell בתיקיית הפרויקט והריצו:

```powershell
dotnet restore MotorcycleAI.sln
```

### 2. יצירת מסד הנתונים וטעינת הנתונים

ודאו ששירות SQL Server Express פועל, והריצו מתיקיית הפרויקט:

```powershell
sqlcmd -S ".\SQLEXPRESS" -E -i "MotorcycleAI.Infrastructure\Database\Scripts\001_CreateMotorcycles.sql"
sqlcmd -S ".\SQLEXPRESS" -E -i "MotorcycleAI.Infrastructure\Database\Scripts\002_SeedIsraeliMotorcycles.sql"
```

הסקריפט הראשון יוצר את מסד הנתונים `MotorcycleAI` ואת הטבלה. השני טוען נתוני דוגמה. אפשר להריץ אותו שוב בבטחה. הנתונים כוללים הערכות מחיר ומאפיינים, ולא כולם נתוני יצרן מאומתים.

אם SQL Server משתמש בשם מופע או בפרטי התחברות אחרים, עדכנו את `Database:ConnectionString` בקובץ `MotorcycleAI.API\appsettings.json`.

### 3. הגדרת והפעלת מודל llama.cpp

#### התקנת llama.cpp והמודל

1. היכנסו לעמוד [הגרסאות של llama.cpp](https://github.com/ggml-org/llama.cpp/releases) והורידו חבילת Windows שמתאימה למחשב שלכם. אם אינכם בטוחים איזו חבילה לבחור, התחילו בגרסת CPU.
2. חלצו את החבילה לתיקייה קבועה, לדוגמה `C:\tools\llama.cpp`, וודאו שבתוכה נמצא הקובץ `llama-server.exe`. אין צורך ב-PATH אם מפעילים אותו באמצעות הנתיב המלא כפי שמודגם למטה.
3. הורידו מודל GGUF שתומך ב-tool calling (למשל Qwen3 Instruct) ממקור אמין. העבירו אותו לתיקייה `C:\models` ועדכנו את נתיב הקובץ בפקודת ההפעלה. המודל וה-chat template חייבים לתמוך בכלי שיחה; מודל הטקסט הקודם של הפרויקט, DictaLM, אינו מובטח לכך.

הגדירו ב-`MotorcycleAI.API\appsettings.json` את כתובת שרת llama.cpp ואת שם המודל:

```json
"LlamaCpp": {
  "BaseUrl": "http://localhost:8080",
  "Model": "local-model",
  "TimeoutSeconds": 1000
}
```

פתחו חלון PowerShell נפרד והפעילו את השרת. התאימו את נתיבי `llama-server.exe` וקובץ ה-GGUF למיקומים שבחרתם:

```powershell
& "C:\tools\llama.cpp\llama-server.exe" --model "C:\models\your-tool-capable-model.gguf" --alias local-model --jinja --cpu-moe --host 127.0.0.1 --port 8080
```

אם מתקבלת הודעה ש-`llama-server.exe` אינו מזוהה, בדרך כלל Windows לא מוצא את הקובץ: בדקו שהוא קיים בתיקייה שחילצתם אליה, ועדכנו את הנתיב המלא בפקודה. ב-PowerShell, אם עברתם לתיקייה שבה נמצא הקובץ, הפעילו אותו עם `.\llama-server.exe` (ולא רק `llama-server.exe`). אפשר גם להוסיף את התיקייה ל-PATH, אך אין בכך צורך כשמשתמשים בנתיב המלא.

השאירו את חלון השרת פתוח בזמן העבודה. `--alias local-model` חייב להתאים לערך `Model` בהגדרות. אפשר לבדוק שהשרת מוכן בכתובת:

```powershell
curl.exe http://localhost:8080/health
```

התשובה הצפויה היא `{"status":"ok"}`. הודעת `actively refused` פירושה שהשרת אינו פועל או שאינו מאזין בפורט שהוגדר.

### 4. הפעלת האפליקציה

בחלון PowerShell נוסף, מתיקיית הפרויקט, הריצו:

```powershell
dotnet run --project MotorcycleAI.API\MotorcycleAI.API.csproj --launch-profile http
```

פתחו את `http://localhost:5150`. ממשק Swagger זמין ב-`http://localhost:5150/swagger`.

## בדיקות

להרצת כל הבדיקות:

```powershell
dotnet test MotorcycleAI.Tests\MotorcycleAI.Tests.csproj
```

## סדר הפעלה בכל שימוש

1. ודאו ש-SQL Server Express פועל.
2. הפעילו את `llama-server.exe` והשאירו אותו רץ.
3. הפעילו את ה-API באמצעות `dotnet run`.
4. פתחו את `http://localhost:5150`.
