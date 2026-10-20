# 參考手冊 05：C# 靜態 AST 語法樹與程式符號提取規格 (Roslyn AST Parsing Spec)

## 一、技術背景與選型理由

在「讀取專案 skills」中，若僅使用純文字正則表示式（Regex）提取 C# 類別與函式資訊，極易受到程式碼換行、泛型巢狀（如 `Task<ActionResult<JSONResultViewModel<T>>>`）、註解干擾或多載函式的影響，導致資訊遺漏或錯誤。

因此，本技能標準採用 **.NET 原生 Roslyn 編譯器平台 API (`Microsoft.CodeAnalysis.CSharp`)** 進行抽象語法樹（Abstract Syntax Tree, AST）靜態解析：
- **百分之百語意準確**：由微軟官方 C# 編譯器解析語法，完全忽視空白與格式排版差異。
- **純靜態離線分析**：不需載入二進位組件至 AppDomain，不執行任何被分析代碼，保障資安安全邊界。
- **結構化提取**：精準提取 XML `/// <summary>` 註解、方法簽章、參數型別與裝飾特性（Attributes）。

---

## 二、Roslyn 語法樹節點對映矩陣

| 目標提取架構項 | Roslyn Syntax 節點型別 | 屬性 / 提取方法 |
| :--- | :--- | :--- |
| **命名空間 (Namespace)** | `NamespaceDeclarationSyntax`<br/>`FileScopedNamespaceDeclarationSyntax` | `.Name.ToString()` |
| **類別宣告 (Class)** | `ClassDeclarationSyntax` | `.Identifier.ValueText`, `.Modifiers` |
| **介面宣告 (Interface)** | `InterfaceDeclarationSyntax` | `.Identifier.ValueText` |
| **公開函式 (Method)** | `MethodDeclarationSyntax` | `.Identifier.ValueText`, `.ReturnType`, `.ParameterList` |
| **輸入參數 (Parameters)** | `ParameterSyntax` | `.Type.ToString()`, `.Identifier.ValueText` |
| **XML 註解摘要** | `DocumentationCommentTriviaSyntax` | 提取 `<summary>` 標籤內之純文字說明 |
| **Linq2DB / MVC 特性** | `AttributeSyntax` | `.Name.ToString()`, `.ArgumentList` |

---

## 三、AST 簽章提取輸出格式規範 (JSON 結構)

靜態語法樹提取器應輸出具備以下 JSON Schema 的結構化資料，以供報告產出引擎自動組裝 Markdown 表格：

```json
{
  "project": "NKUST.Service",
  "filePath": "Login/SignService.cs",
  "namespace": "NKUST.Service.Login",
  "classes": [
    {
      "className": "SignService",
      "inherits": "BaseService, ISignService",
      "summary": "處理使用者認證、SSO 代登票券校驗與登出核心邏輯",
      "methods": [
        {
          "name": "VerifySignTicketAsync",
          "access": "public",
          "isAsync": true,
          "returnType": "Task<UserViewModel>",
          "parameters": [
            { "name": "ticket", "type": "string", "summary": "由校務 Portal 傳遞之 60 秒動態代登票券" },
            { "name": "clientIp", "type": "string", "summary": "客戶端來源 IP" }
          ],
          "summary": "校驗 Sybase s90_auth_log 票券，成功後組裝身分 Claims 快照",
          "sideEffects": ["讀取 Sybase s90_auth_log 靜態定義", "比對有效時間戳"]
        }
      ]
    }
  ]
}
```

---

## 四、Roslyn 語法分析核心演算法代碼 (C# Implementation)

```csharp
using System;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

public class RoslynAstExtractor
{
    public static void ParseCSharpFile(string filePath)
    {
        string sourceCode = File.ReadAllText(filePath);
        SyntaxTree tree = CSharpSyntaxTree.ParseText(sourceCode);
        CompilationUnitSyntax root = tree.GetCompilationUnitRoot();

        var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>();
        foreach (var cls in classes)
        {
            Console.WriteLine($"[Class] {cls.Identifier.ValueText}");

            var methods = cls.DescendantNodes().OfType<MethodDeclarationSyntax>()
                             .Where(m => m.Modifiers.Any(SyntaxKind.PublicKeyword));

            foreach (var m in methods)
            {
                string retType = m.ReturnType.ToString();
                string methodName = m.Identifier.ValueText;
                var paramList = string.Join(", ", m.ParameterList.Parameters.Select(p => $"{p.Type} {p.Identifier.ValueText}"));
                
                Console.WriteLine($"  - Method: {retType} {methodName}({paramList})");
            }
        }
    }
}
```
