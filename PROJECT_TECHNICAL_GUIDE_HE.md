# MotorcycleAI — מדריך טכני מעמיק

## 1. מטרת המדריך

המדריך מתאר את המימוש הקיים בפרויקט: ארכיטקטורה, אחריות הקבצים, מסלול הבקשה, מודלי הנתונים, חוזי ה-API, דירוג, מסד נתונים, חיבור למודל, טיפול בשגיאות ובדיקות.

למבוא קצר למוצר ולרעיון, ראו את [מסמך האפיון וההסבר](./PROJECT_SPEC_HE.md). להתקנה ראשונית מלאה, כולל הכנת SQL Server והפעלת llama.cpp, ראו את [README](./README.md).

## 2. מבנה הפתרון

הפתרון `MotorcycleAI.sln` מכיל ארבעה פרויקטים:

```text
MotorcycleAI.sln
├── MotorcycleAI.API
│   ├── Controllers/                 נקודות קצה
│   ├── Contracts/                   DTOs לבקשות ולתשובות
│   └── wwwroot/                     ממשק Web סטטי
├── MotorcycleAI.Core
│   ├── Interfaces/                  חוזים בין הליבה לתשתית
│   ├── Models/                      מודלים עסקיים
│   └── Services/                    ניתוח, ולידציה והמלצות
├── MotorcycleAI.Infrastructure
│   ├── Repositories/                SQL Server ושאילתות
│   ├── LlamaCpp/                    מתאם ל-llama.cpp
│   ├── Ollama/                      מתאם ל-Ollama
│   └── Database/Scripts/            יצירת טבלה ונתוני דוגמה
└── MotorcycleAI.Tests
    ├── Unit/
    └── Integration/
```

### כיוון התלויות

- `API` תלוי ב-`Core` וב-`Infrastructure`.
- `Infrastructure` תלוי ב-`Core`, כדי לממש את הממשקים שהליבה מגדירה.
- `Core` אינו תלוי ב-API או בפרטי SQL/HTTP.
- `Tests` בודק את שלושת הפרויקטים באמצעות בדיקות יחידה ואינטגרציה.

`Program.cs` רושם את שירותי ASP.NET, טיפול השגיאות, שירותי התשתית, `SearchAnalyzer` ו-`RecommendationService`. ה-Controller תלוי בממשקים ולא במימושים קונקרטיים של המודל/מסד הנתונים.

## 3. מסלול בקשת חיפוש — מקצה לקצה

נקודת הכניסה היא `POST /api/motorcycles/search`.

### שלב א: קבלת הבקשה

ה-Controller מקבל `SearchMotorcyclesRequestDto`, שבו השדה היחיד הוא:

| שדה | סוג | תנאים |
|---|---|---|
| `query` | מחרוזת | נדרש; 3–2,000 תווים |

`[ApiController]` מפעיל ולידציה אוטומטית של Data Annotations. גוף לא תקין נדחה לפני הפעלת שירותי הליבה.

### שלב ב: הפיכת טקסט לדרישות מובְנות

`ISearchAnalyzer.AnalyzeAsync` מקבל את הטקסט. המימוש `SearchAnalyzer` שולח ל-`IStructuredResponseGenerator`:

- הוראות מערכת בעברית: לחלץ דרישות בלבד, לא להמציא פרטים, ולהשאיר `null` בשדות שלא צוינו.
- טקסט המשתמש.
- JSON Schema שמגדירה את סוגי השדות, ערכי `null` מותרים, שדות נדרשים ואיסור על שדות נוספים.

המודל נדרש להחזיר אובייקט עם כל 14 המאפיינים הבאים:

| מאפיין | סוג | משמעות |
|---|---|---|
| `VehicleType` | `string?` | סוג כלי הרכב |
| `MinPrice`, `MaxPrice` | `decimal?` | גבולות מחיר |
| `MinEngineCC`, `MaxEngineCC` | `int?` | גבולות נפח מנוע |
| `MinYear`, `MaxYear` | `int?` | גבולות שנתון |
| `RiderExperience` | `string?` | רמת ניסיון |
| `Usage` | `string?` | שימוש מתוכנן |
| `MotorcycleType` | `string?` | סוג/קטגוריית אופנוע |
| `PreferredBrands` | `IReadOnlyList<string>?` | מותגים מועדפים |
| `Reliability` | `string?` | דרישת אמינות |
| `MaintenanceCost` | `string?` | דרישת עלות תחזוקה |
| `AdditionalPreferences` | `string?` | העדפות נוספות בטקסט |

לאחר קבלת התשובה, `SearchAnalyzer` בודק שהתגובה אינה ריקה, שהיא אובייקט JSON, שכל 14 המאפיינים מופיעים ושניתן להמיר אותה ל-`MotorcycleSearchRequest`. לאחר מכן `MotorcycleSearchRequestValidator` בודק:

- מחיר לא שלילי.
- נפח מנוע בין 0 ל-10,000 סמ״ק.
- שנתון בין 1885 לשנה הבאה לכל היותר.
- טווחים תקינים: ערך מינימום אינו גדול מערך מקסימום.

שגיאת JSON או מבנה של המודל הופכת ל-`InvalidDataException`; הטיפול הגלובלי מחזיר 502.

### שלב ג: איתור מועמדים ב-SQL Server

ה-Controller שולח את `MotorcycleSearchRequest` אל `IMotorcycleRepository.SearchAsync`. המימוש `MotorcycleRepository` פותח `SqlConnection`, ו-`MotorcycleSearchQueryBuilder` מרכיב את השאילתה ואת הפרמטרים.

**מסננים קשיחים שנוספים ל-`WHERE`:**

- `Price >= MinPrice` ו-`Price <= MaxPrice`
- `EngineCC >= MinEngineCC` ו-`EngineCC <= MaxEngineCC`
- `[Year] >= MinYear` ו-`[Year] <= MaxYear`

כל תנאי נוסף רק אם נמסר ערך. ערכי משתמש מועברים כפרמטרים של Dapper ואינם משורשרים לתוך SQL.

**מיון לפי העדפות:** אם נמסרו ערכים, השאילתה נותנת קדימות למועמדים שתואמים, בסדר הבא: `VehicleType`, `MotorcycleType`, `RiderExperience`, `Usage`, `Reliability`, `MaintenanceCost`, מותג מועדף. לאחר מכן המיון הוא מחיר עולה, שנתון יורד, יצרן ומודל בסדר עולה.

העדפות אלה אינן תנאי `WHERE`: אופנוע שאינו תואם להן עדיין יכול להופיע בתוצאות. `AdditionalPreferences` אינו מטופל בשאילתה.

### שלב ד: חישוב ציון והפקת רשימת המועמדים הסופית

`RecommendationService` מבצע הערכה דטרמיניסטית לכל אופנוע שהוחזר מהמאגר. עבור כל קריטריון שהוא יודע לבדוק הוא מוסיף נקודה למכנה; התאמה מוסיפה נקודה גם למונה. הציון הוא:

```text
100 × מספר הקריטריונים התואמים / מספר הקריטריונים שנבדקו
```

הציון מעוגל לשתי ספרות אחרי הנקודה. כשאין קריטריונים שנבדקו, הציון הוא 50.

**קריטריונים בציון:**

- גבולות מחיר, נפח מנוע ושנתון — כל טווח שסופק נחשב קריטריון אחד.
- ערכי סוג כלי, סוג אופנוע, ניסיון, שימוש, אמינות ועלות תחזוקה — נבדקים כהשוואת מחרוזת מדויקת לאחר `Trim`, ללא תלות באותיות גדולות/קטנות.
- מותגים — נחשב קריטריון אחד, ומותג מתאים אם אחד הערכים תואם ליצרן.

`Not rated` ו-`Unknown` בשדה טקסטואלי מטופלים כמידע לא מאומת: נרשם חיסרון מתאים, אך הערך לא נכנס למכנה ולכן אינו מוריד ציון. ערך חסר אחר, כשנדרש, יוצר חיסרון ונחשב אי-התאמה.

לאחר מכן הרשימה ממוינת לפי ציון יורד, מחיר עולה ושנתון יורד. רק שלושת הראשונים מועברים לשלב ההסבר. אם אין אופנועים, מוחזר סיכום קבוע בלי לפנות למודל פעם נוספת.

### שלב ה: יצירת הסברים ואימות תגובת המודל

כאשר יש מועמדים, `RecommendationService` שולח למודל את הבקשה המקורית, הדרישות שחולצו ופרטי המועמדים שנבחרו. הוא כולל בהקשר גם את רשימות `SupportedAdvantages` ו-`SupportedDisadvantages` שנוצרו בקוד.

המודל מתבקש להחזיר:

- `summary` — סיכום לא ריק.
- `recommendations` — אובייקט שבו לכל מזהה מועמד יש `reason` לא ריק אחד בלבד.

הסכימה מתירה רק את מזהי המועמדים שנשלחו ומחייבת את כולם. הקוד מאמת שוב שהתגובה היא JSON תקין, שכל מזהה קיים ומופיע פעם אחת, ושלא חסרים מועמדים. יתרונות, חסרונות וציון נלקחים מהקוד ולא מהמודל. כך מונעים מהמודל להוסיף אופנועים, להמציא ציון או לשנות את רשימות היתרונות והחסרונות.

### שלב ו: הרכבת תשובת API והצגה

ה-Controller ממיר את המודלים העסקיים ל-DTOs, מצרף `SearchRequest`, רשימת המלצות וסיכום ומחזיר HTTP 200. ה-UI מציג את התקציר, כרטיס לכל המלצה, מחיר, ציון, סיבה, יתרונות וחסרונות. אם אין תוצאות, הוא מציג הודעת ניסיון מחדש.

## 4. מודלים וחוזים עיקריים

### מודלים ב-Core

- `Motorcycle`: רשומת אופנוע, כולל מזהה, יצרן, מודל, שנתון, סמ״ק, מחיר, דגלי אומדן, קטגוריות, תיאור ותמונה.
- `MotorcycleSearchRequest`: הדרישות שחולצו מהטקסט.
- `Recommendation`: מזהה אופנוע, ציון, סיבה ורשימות יתרונות/חסרונות.
- `RecommendationResult`: אוסף המלצות וסיכום.

### DTOs ב-API

- `SearchMotorcyclesRequestDto`: גוף בקשת החיפוש, עם `Query`.
- `MotorcycleSearchRequestDto`: החזרת הדרישות שחולצו, כדי לאפשר לממשק/לקליינט לראות מה נותח.
- `MotorcycleSearchResponseDto`: אובייקט התשובה הכולל `SearchRequest`, `Recommendations` ו-`Summary`.
- `MotorcycleRecommendationDto`: נתוני התאמה והסבר, יחד עם אובייקט האופנוע.
- `MotorcycleDetailsDto`: פרטי אופנוע, המשמשים גם בתוך המלצה וגם ב-GET לפי מזהה.

ב-JSON של ASP.NET Core שמות המאפיינים מופיעים כברירת מחדל ב-camelCase, לדוגמה `matchScore` ו-`motorcycleId`.

## 5. חוזי HTTP

### חיפוש: `POST /api/motorcycles/search`

בקשה:

```json
{
  "query": "אני מתחיל, מחפש אופנוע עד 30 אלף שקל לנסיעות בעיר"
}
```

תשובת הצלחה לדוגמה — ערכים להמחשה בלבד:

```json
{
  "searchRequest": {
    "maxPrice": 30000,
    "riderExperience": "מתחיל",
    "usage": "עיר",
    "preferredBrands": null
  },
  "recommendations": [
    {
      "motorcycleId": 12,
      "rankLabel": "ההתאמה הטובה ביותר",
      "matchScore": 100,
      "motorcycle": {
        "id": 12,
        "manufacturer": "Honda",
        "model": "דוגמה",
        "year": 2024,
        "engineCC": 350,
        "price": 25000,
        "priceIsEstimate": false,
        "attributesAreEstimated": false
      },
      "reason": "מתאים לדרישות שצוינו.",
      "advantages": ["המחיר עומד בטווח שהוגדר."],
      "disadvantages": []
    }
  ],
  "summary": "נמצאה אפשרות מתאימה."
}
```

השדות שלא מופיעים בדוגמה קיימים בחוזה המלא, ובשדות חיפוש שלא זוהו מתקבל `null` או רשימה ריקה לפי התוצאה. התשובה בפועל תלויה בנתוני המאגר ובמודל.

קודי שגיאה אפשריים:

| HTTP | משמעות |
|---|---|
| 400 | גוף בקשה לא תקין או ערך/דרישה לא תקינים |
| 502 | המודל החזיר JSON/מבנה לא תקין |
| 503 | שירות המודל אינו זמין או החזיר כשל HTTP |
| 504 | שירות המודל לא הגיב בזמן |
| 500 | שגיאה לא צפויה |

שגיאות המטופלות על ידי `GlobalExceptionHandler` מחזירות `ProblemDetails`, בלי פרטי חריגה פנימיים, וכוללות `traceId`. שגיאת ולידציה אוטומטית של ASP.NET Core עשויה לקבל גוף `ValidationProblemDetails`.

### פרטי אופנוע: `GET /api/motorcycles/{id}`

- מזהה חיובי קיים: 200 עם `MotorcycleDetailsDto`.
- מזהה חיובי שאינו קיים: 404.
- מזהה שאינו חיובי: 400 עם `ProblemDetails`.

### ממשקי פיתוח

- ממשק Web מוגש מתוך `MotorcycleAI.API/wwwroot`; `UseDefaultFiles` ו-`UseStaticFiles` מאפשרים לפתוח את דף הבית.
- Swagger מופעל רק כאשר סביבת ASP.NET היא `Development`, בנתיב `/swagger`.
- הכתובת `http://localhost:5150` מוגדרת בפרופיל `http` שב-`Properties/launchSettings.json`.

## 6. מסד הנתונים

הסקריפט `001_CreateMotorcycles.sql` יוצר (אם אינם קיימים) את מסד הנתונים `MotorcycleAI`, את הטבלה `dbo.Motorcycles` ואת האינדקס `IX_Motorcycles_Search` על מחיר, נפח מנוע ושנתון.

| עמודה | SQL | ריקה? | שימוש |
|---|---|---|---|
| `Id` | `INT IDENTITY` | לא | מפתח ראשי |
| `Manufacturer`, `Model` | `NVARCHAR(100)` | לא | יצרן ודגם |
| `Year` | `INT` | לא | שנתון, 1885–2200 |
| `EngineCC` | `INT` | לא | סמ״ק, 0–10,000 |
| `Price` | `DECIMAL(12,2)` | לא | מחיר, 0 ומעלה |
| `PriceIsEstimate` | `BIT` | לא | האם המחיר אומדן |
| `AttributesAreEstimated` | `BIT` | לא | האם מאפייני התאמה אומדניים |
| `VehicleType` | `NVARCHAR(50)` | כן | סוג כלי |
| `MotorcycleType` | `NVARCHAR(50)` | לא | סוג אופנוע |
| `RiderExperience` | `NVARCHAR(50)` | כן | ניסיון |
| `Usage` | `NVARCHAR(100)` | כן | שימוש |
| `Reliability` | `NVARCHAR(30)` | כן | אמינות |
| `MaintenanceCost` | `NVARCHAR(30)` | כן | עלות תחזוקה |
| `Description` | `NVARCHAR(1000)` | כן | תיאור |
| `ImageUrl` | `NVARCHAR(2048)` | כן | קישור לתמונה |

קיימת גם מגבלת ייחודיות על `Manufacturer`, `Model`, `Year`. הסקריפט `002_SeedIsraeliMotorcycles.sql` טוען נתוני דוגמה וכולל סימון של נתונים משוערים. יש להתייחס לנתוני הדוגמה כמידע המחשה, לא כקטלוג מוסמך.

הגישה למסד נעשית עם `Microsoft.Data.SqlClient` ו-Dapper. יצירת schema וטעינת נתוני דוגמה מתבצעות בסקריפטים ידניים; אין בפרויקט שכבת migrations של Entity Framework.

## 7. חיבור למודל שפה

החוזה המופשט הוא `IStructuredResponseGenerator`. מימושי התשתית:

- `LlamaCppService`: שולח בקשת chat אל `v1/chat/completions`, עם JSON Schema ב-`response_format`.
- `OllamaService`: שולח בקשת chat אל `api/chat`, עם JSON Schema ב-`format`.

שני המימושים עובדים באופן סינכרוני מבחינת התשובה (`stream: false`), משתמשים ב-`HttpClient` עם timeout, וממירים כשלי תקשורת, timeout ותגובה לא תקינה לחריגות ברורות.

ב-`DependencyInjection.AddInfrastructure` נבחר llama.cpp אם קיימת הגדרת `LlamaCpp`; אחרת נבחר Ollama. בקובץ `MotorcycleAI.API/appsettings.json` של הפרויקט קיימת כרגע הגדרת `LlamaCpp`, ולכן זו ברירת המחדל במצב הנוכחי.

הגדרות llama.cpp:

```json
"LlamaCpp": {
  "BaseUrl": "http://localhost:8080",
  "Model": "local-model",
  "TimeoutSeconds": 1000
}
```

`BaseUrl` חייב להיות URL מוחלט מסוג HTTP/HTTPS, שם המודל חייב להיות לא ריק וה-timeout חיובי. הגדרת `Model` חייבת להתאים ל-alias שהשרת המקומי מציג.

למעבר ל-Ollama יש להסיר/לא לספק את מקטע `LlamaCpp` ולספק מקטע `Ollama` עם `BaseUrl`, `Model` ו-`TimeoutSeconds`. שימו לב שספקי ההגדרות הרגילים של ASP.NET Core יכולים לדרוס ערכים מקובץ JSON, למשל באמצעות משתני סביבה.

## 8. קובצי מימוש לפי אחריות

### `MotorcycleAI.API`

- `Program.cs` — רישום Dependency Injection, Swagger, טיפול שגיאות, קבצים סטטיים ו-Controllers.
- `Controllers/MotorcyclesController.cs` — תזמור זרימת החיפוש, DTO mapping, דירוג תצוגתי `RankLabel` ו-GET לפי מזהה.
- `GlobalExceptionHandler.cs` — מיפוי חריגות ל-HTTP/ProblemDetails ורישום לוגים.
- `Contracts/Requests` — DTOs לבקשות HTTP.
- `Contracts/Responses` — DTOs לתשובות HTTP.
- `wwwroot/index.html`, `app.js`, `app.css` — ממשק עברי, טופס חיפוש, תצוגת טעינה וכרטיסי תוצאות. `app.js` בונה תוכן משתמש באמצעות `textContent` ולא מפרש אותו כ-HTML.
- `Properties/launchSettings.json` — פרופיל פיתוח בכתובת `localhost:5150`.
- `appsettings.json` — הגדרות ברירת מחדל ל-Logging, llama.cpp ומסד הנתונים.

### `MotorcycleAI.Core`

- `Interfaces/IMotorcycleRepository.cs` — חיפוש ושליפה לפי מזהה.
- `Interfaces/ISearchAnalyzer.cs` — חילוץ דרישות מבקשת טקסט.
- `Interfaces/IRecommendationService.cs` — יצירת תוצאה מדורגת.
- `Interfaces/IStructuredResponseGenerator.cs` — חוזה אחיד ליצירת תשובה לפי JSON Schema.
- `Models` — מודלים עסקיים שאינם תלויים ב-HTTP או SQL.
- `Services/SearchAnalyzer.cs` — prompt, סכימת JSON, parsing ובדיקת מבנה.
- `Services/MotorcycleSearchRequestValidator.cs` — גבולות חוקיים למחיר, שנתון ונפח.
- `Services/RecommendationService.cs` — ציון, יתרונות/חסרונות, בחירת שלושת המועמדים ואימות הסבר המודל.

### `MotorcycleAI.Infrastructure`

- `DependencyInjection.cs` — בחירת ספק המודל, ולידציית Options ורישום Repository/HttpClient.
- `Repositories/MotorcycleRepository.cs` — חיבור SQL והמרת שורות ל-`Motorcycle` באמצעות Dapper.
- `Repositories/MotorcycleSearchQueryBuilder.cs` — בניית SQL עם פרמטרים, מסנני טווח ומיון העדפות.
- `Database/DatabaseOptions.cs` — הגדרות Connection String.
- `LlamaCpp` ו-`Ollama` — Options ומתאמים לפרוטוקול HTTP של כל ספק.
- `Database/Scripts` — יצירת מסד/טבלה/אינדקס ונתוני דוגמה.

### `MotorcycleAI.Tests`

- `Unit/SearchAnalyzerTests.cs` — חילוץ JSON, nulls, schema ותגובות שגויות.
- `Unit/MotorcycleSearchRequestValidatorTests.cs` — גבולות וערכים לא חוקיים.
- `Unit/MotorcycleSearchQueryBuilderTests.cs` — בניית פילטרים, פרמטריזציה והשמטת פילטרים ריקים.
- `Unit/RecommendationServiceTests.cs` — ציונים, מיון, מגבלת שלוש המלצות, נתונים לא מדורגים ואימות תגובת המודל.
- `Unit/LlamaCppServiceTests.cs`, `Unit/OllamaServiceTests.cs` — חוזי בקשה/תגובה והתנהגות מתאמי המודל.
- `Integration/MotorcyclesApiTests.cs` — צינור ה-API עם שירותים מוחלפים ב-mocks.

בדיקות ה-API אינן דורשות להפעיל בפועל SQL Server או מודל, משום שהממשקים מוחלפים ב-mocks. בדיקות המתאמים משתמשות בתגובות HTTP מדומות.

## 9. ממשק המשתמש

הממשק הוא HTML, CSS ו-JavaScript רגילים; אין צורך ב-Node.js או בבניית frontend נפרדת. הוא:

1. אוסף את הטקסט מה-`textarea`.
2. שולח `fetch` ל-`/api/motorcycles/search` עם JSON.
3. מציג הודעת טעינה, משבית את הטופס בזמן הבקשה ומחזיר אותו לפעילות בסיום.
4. בודק את קוד ה-HTTP ואת מבנה `recommendations`.
5. מציג הסבר, דירוג, מחיר ונתוני אומדן.

`Enter` שולח את הבקשה, ו-`Shift+Enter` מאפשר ירידת שורה. לחצן ההצעה הראשוני ממלא את שדה הקלט אך אינו שולח את הבקשה.

## 10. טיפול בכשלים ולוגים

`GlobalExceptionHandler` ממפה:

- `ArgumentException` או `ValidationException` ל-400.
- `TimeoutException` ל-504.
- `HttpRequestException` ל-503.
- `InvalidDataException` ל-502.
- חריגות אחרות ל-500.

התגובה כוללת כותרת, פירוט כללי, נתיב ו-`traceId`; היא אינה חושפת את פרטי החריגה הפנימיים. חריגות 5xx נרשמות ברמת Error ו-4xx ברמת Warning. בקשה שבוטלה על ידי הלקוח אינה מומרת לתשובת שגיאה חדשה על ידי ה-handler.

## 11. הרצה ובדיקות

הפרויקט מכוון ל-`.NET 10` (`net10.0`). לריצה מלאה נדרשים SQL Server והמימוש של ספק המודל שהוגדר. צעדי התקנה מפורטים ב-[README](./README.md). פקודות מרכזיות מ-PowerShell בתיקיית השורש:

```powershell
dotnet restore MotorcycleAI.sln
dotnet test MotorcycleAI.Tests\MotorcycleAI.Tests.csproj
dotnet run --project MotorcycleAI.API\MotorcycleAI.API.csproj --launch-profile http
```

כתובות פיתוח:

- ממשק: `http://localhost:5150`
- Swagger: `http://localhost:5150/swagger` (בסביבת Development)
- llama.cpp, לפי ברירת המחדל: `http://localhost:8080`

סקריפטי SQL:

```powershell
sqlcmd -S ".\SQLEXPRESS" -E -i "MotorcycleAI.Infrastructure\Database\Scripts\001_CreateMotorcycles.sql"
sqlcmd -S ".\SQLEXPRESS" -E -i "MotorcycleAI.Infrastructure\Database\Scripts\002_SeedIsraeliMotorcycles.sql"
```

`dotnet test` מריץ את בדיקות הפרויקט. אין צורך להפעיל את מסד הנתונים או את המודל עבור הבדיקות הקיימות, בזכות mocks ו-HTTP handlers מדומים.

## 12. מגבלות, הנחות ונקודות להמשך

אלה תצפיות על המימוש הנוכחי ולא טענה שהיכולות כבר קיימות:

- `AdditionalPreferences` אינו נבדק באופן מובנה בשאילתת המאגר או בציון. הוא נשלח בהקשר להסבר, אך אין הבטחה שיתורגם להתאמה עקבית.
- התאמת שדות טקסטואליים היא שוויון מחרוזות ולא התאמה סמנטית; ניסוחים שקולים עלולים שלא להיחשב שווים.
- מסנני SQL קשיחים קיימים רק למחיר, נפח מנוע ושנתון; אין `LIMIT` בשאילתת המאגר, ורק אחרי טעינת המועמדים נבחרים שלושה.
- ציון ההתאמה נותן משקל שווה לכל קריטריון שנבדק, ללא משקל שונה להעדפות חשובות יותר.
- ממשק השיחה אינו מנהל session או הקשר בין חיפושים.
- נתוני דוגמה והערכות אינם מקור אמת למחיר/מפרט; יש לאמתם לפני החלטה מעשית.
- אין endpoint לניהול/כתיבה של נתוני אופנועים, אימות משתמשים או הרשאות בפרויקט הנוכחי.
- הקובץ `MotorcycleAI.API.http` מכיל כרגע בקשת `weatherforecast`, בעוד שה-API הממומש בפרויקט אינו מגדיר נקודת קצה כזו. לצורך בדיקות ידניות השתמשו בדוגמאות `/api/motorcycles/search` וב-`/api/motorcycles/{id}` במסמך זה או ב-Swagger.

## 13. מילון מונחים

| מונח | הסבר |
|---|---|
| API | ממשק HTTP שבאמצעותו הממשק או לקוח אחר מתקשרים עם השרת |
| DTO | אובייקט להעברת נתונים בין שכבות/על גבי HTTP |
| JSON Schema | תיאור של מבנה JSON צפוי וסוגי השדות שלו |
| Dependency Injection | מנגנון לרישום והעברת שירותים למי שתלוי בהם |
| Repository | רכיב שמבודד את קוד הליבה מגישה למסד נתונים |
| Adapter | רכיב שמתרגם חוזה פנימי לפרוטוקול של ספק חיצוני, כגון llama.cpp |
| Candidate | אופנוע שהוחזר מהמאגר ונבדק לצורך דירוג |
| MatchScore | אחוז הקריטריונים שנבדקו ונמצאו תואמים |
| Estimated data | נתון משוער שאינו מוצג כעובדה מאומתת |
