# MotorcycleAI

MotorcycleAI היא אפליקציית Web בעברית שמסייעת למצוא אופנועים לפי צרכי המשתמש. היא מנתחת בקשות בשפה טבעית ומחזירה המלצות מתוך מאגר האופנועים.

## מסמכי הפרויקט

- [מסמך אפיון והסבר בעברית](./PROJECT_SPEC_HE.md) — הרעיון, המשתמשים וזרימת המערכת.
- [מדריך טכני מעמיק בעברית](./PROJECT_TECHNICAL_GUIDE_HE.md) — ארכיטקטורה, API, מודלים, דירוג, מסד נתונים ובדיקות.

## רכיבי הפרויקט

- **MotorcycleAI.API** – API ואפליקציית Web סטטית, זמינים כברירת מחדל בכתובת `http://localhost:5150`.
- **MotorcycleAI.Core** – מודלים, ממשקים ולוגיקת ניתוח והמלצות.
- **MotorcycleAI.Infrastructure** – גישה ל-SQL Server וחיבור למודל דרך llama.cpp או Ollama.
- **MotorcycleAI.Tests** – בדיקות יחידה ואינטגרציה.

## מה צריך להתקין

1. **.NET 10 SDK** – נדרש לבנייה ולהרצה של הפרויקט.
2. **SQL Server Express** – יש ליצור או להתקין מופע בשם `SQLEXPRESS`.
3. **SQL Server Command Line Utilities (`sqlcmd`) או SQL Server Management Studio (SSMS)** – להרצת סקריפטי יצירת מסד הנתונים.
4. **ספק מודל אחד לבחירתכם:**
   - **llama.cpp** ל-Windows ו-**מודל GGUF** תואם. לדוגמה, הפרויקט נבדק עם DictaLM 3.0 1.7B Instruct בקוונטיזציית Q4_K_M.
   - **Ollama** ל-Windows ומודל שהורדתם באמצעות Ollama.

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

### 3. בחירת ספק המודל והפעלתו

אפשר להשתמש ב-**llama.cpp** או ב-**Ollama**. אין צורך בשינוי קוד, אבל צריך להגדיר ולהפעיל ספק אחד. האפליקציה בוחרת ב-llama.cpp כאשר קיים מקטע `LlamaCpp` בהגדרות; אם המקטע אינו קיים, היא משתמשת במקטע `Ollama`. לכן, כדי לבחור ב-Ollama, יש להסיר את `LlamaCpp` מ-`MotorcycleAI.API\appsettings.json` ורק להוסיף הגדרת `Ollama`. לאחר שינוי ההגדרות הפעילו מחדש את ה-API.

#### אפשרות א׳: llama.cpp

##### התקנת llama.cpp והמודל

1. היכנסו לעמוד [הגרסאות של llama.cpp](https://github.com/ggml-org/llama.cpp/releases) והורידו חבילת Windows שמתאימה למחשב שלכם. אם אינכם בטוחים איזו חבילה לבחור, התחילו בגרסת CPU.
2. חלצו את החבילה לתיקייה קבועה, לדוגמה `C:\tools\llama.cpp`, וודאו שבתוכה נמצא הקובץ `llama-server.exe`. אין צורך ב-PATH אם מפעילים אותו באמצעות הנתיב המלא כפי שמודגם למטה.
3. הורידו את קובץ המודל:
   - פתחו את [עמוד DictaLM 3.0 1.7B Instruct Q4_K_M ב-Hugging Face](https://huggingface.co/EMD123/DictaLM-3.0-1.7B-Instruct-Q4_K_M-GGUF).
   - עברו ללשונית **Files and versions** ולחצו על `dictalm-3.0-1.7b-instruct-q4_k_m.gguf` כדי להוריד אותו. אפשר גם להשתמש [בקישור הישיר לקובץ](https://huggingface.co/EMD123/DictaLM-3.0-1.7B-Instruct-Q4_K_M-GGUF/resolve/main/dictalm-3.0-1.7b-instruct-q4_k_m.gguf?download=true).
   - זהו קובץ גדול (כ-1.1 GB); המתינו לסיום ההורדה. העבירו אותו לתיקייה `C:\models` (צרו אותה אם אינה קיימת), כך שהנתיב המלא יהיה `C:\models\dictalm-3.0-1.7b-instruct-q4_k_m.gguf`.
   - ודאו שהקובץ ירד במלואו ושסיומתו `.gguf`, ולא נשמר בתוך תיקיית Downloads או תחת שם אחר. הפקודה הבאה מצפה בדיוק לנתיב הזה. זהו קובץ GGUF של המרה קהילתית; פרטי המודל המקורי מופיעים בעמוד המודל.

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
& "C:\tools\llama.cpp\llama-server.exe" --model "C:\models\dictalm-3.0-1.7b-instruct-q4_k_m.gguf" --alias local-model --host 127.0.0.1 --port 8080
```

אם מתקבלת הודעה ש-`llama-server.exe` אינו מזוהה, בדרך כלל Windows לא מוצא את הקובץ: בדקו שהוא קיים בתיקייה שחילצתם אליה, ועדכנו את הנתיב המלא בפקודה. ב-PowerShell, אם עברתם לתיקייה שבה נמצא הקובץ, הפעילו אותו עם `.\llama-server.exe` (ולא רק `llama-server.exe`). אפשר גם להוסיף את התיקייה ל-PATH, אך אין בכך צורך כשמשתמשים בנתיב המלא.

השאירו את חלון השרת פתוח בזמן העבודה. `--alias local-model` חייב להתאים לערך `Model` בהגדרות. אפשר לבדוק שהשרת מוכן בכתובת:

```powershell
curl.exe http://localhost:8080/health
```

התשובה הצפויה היא `{"status":"ok"}`. הודעת `actively refused` פירושה שהשרת אינו פועל או שאינו מאזין בפורט שהוגדר.

#### אפשרות ב׳: Ollama

1. התקינו את Ollama ל-Windows מהאתר [ollama.com/download](https://ollama.com/download/windows).
2. פתחו PowerShell והורידו מודל. לדוגמה:

   ```powershell
   ollama pull qwen2.5:3b
   ```

   אפשר לבחור מודל אחר, אך יש לוודא שהוא מותקן ושמתאים ליצירת תשובות JSON מובְנות. הציגו את המודלים המותקנים באמצעות:

   ```powershell
   ollama list
   ```

3. ודאו ששרת Ollama פועל. בדרך כלל תוכנת Ollama מפעילה אותו ברקע. אם הוא אינו פועל, הריצו בחלון PowerShell:

   ```powershell
   ollama serve
   ```

   השאירו את השרת פועל. בדקו את החיבור:

   ```powershell
   curl.exe http://localhost:11434/api/tags
   ```

   תשובת JSON עם רשימת המודלים מעידה שהשרת נגיש.

4. פתחו את `MotorcycleAI.API\appsettings.json`. השאירו את ההגדרות `Logging` ו-`Database` ללא שינוי, הסירו את מקטע `LlamaCpp` והגדירו במקום זאת את Ollama:

   ```json
   "Ollama": {
     "BaseUrl": "http://localhost:11434",
     "Model": "qwen2.5:3b",
     "TimeoutSeconds": 1000
   }
   ```

   הערך `Model` חייב להתאים בדיוק לשם שמופיע ב-`ollama list`. אם בחרתם מודל אחר, עדכנו את הערך בהתאם. לאחר מכן הפעילו את ה-API מחדש.

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
2. הפעילו את ספק המודל שהגדרתם והשאירו אותו רץ: `llama-server.exe` עבור llama.cpp או שרת Ollama.
3. הפעילו את ה-API באמצעות `dotnet run`.
4. פתחו את `http://localhost:5150`.
