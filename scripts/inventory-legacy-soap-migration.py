"""從舊原始碼建立移植對照；只輸出方法、依賴與欄位名稱，不輸出連線資訊或密碼。"""
import argparse
import hashlib
import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
# 保留字串供辨識連線設定名稱；排除註解中的停用程式。
TOKENS = re.compile(r'@"(?:""|[^"])*"|"(?:\\.|[^"\\])*"|\x27(?:\\.|[^\x27\\])*\x27|//[^\n]*|/\*[\s\S]*?\*/')


def active_code(source):
    return TOKENS.sub(lambda m: re.sub(r"[^\n]", " ", m[0]) if m[0].startswith(("//", "/*")) else m[0], source)


def find_method(source, name):
    clean = active_code(source)
    signature = re.search(r"\bpublic\s+(?:override\s+)?([\w<>]+)\s+" + re.escape(name) + r"\s*\((.*?)\)\s*\{", clean, re.S)
    if not signature:
        raise ValueError("找不到舊方法：" + name)
    start = signature.end()
    # 字串內的大括號不參與方法結尾計算。
    masked = TOKENS.sub(lambda m: re.sub(r"[^\n]", " ", m[0]), clean)
    depth, end = 1, start
    while depth and end < len(masked):
        depth += (masked[end] == "{") - (masked[end] == "}")
        end += 1
    if depth:
        raise ValueError("方法大括號不完整：" + name)
    parameters = re.findall(r"\bstring\s+(\w+)", signature[2])
    return {
        "line": source.count("\n", 0, signature.start()) + 1,
        "returnType": signature[1], "parameters": parameters,
        "body": clean[start:end - 1],
    }


def dependencies(body):
    return {
        "connectionSettings": sorted(set(re.findall(r'ConnectionStrings\[\s*"([^"]+)"\s*\]', body))),
        "ldapSettings": sorted(set(re.findall(r'AppSettings\[\s*"(Ldap[^"]+)"\s*\]', body))),
        "tables": sorted(set(re.findall(r"\b(?:FROM|JOIN|INTO|UPDATE)\s+([\w.]+)", body, re.I))),
    }


def inventory(legacy):
    source_file = legacy / "KuasSSOWebService.asmx.cs"
    helper_file = legacy / "Module/LdapAuthentication.cs"
    source = source_file.read_text(encoding="utf-8-sig")
    helpers = helper_file.read_text(encoding="utf-8-sig")
    catalog = json.loads((ROOT / "NkustSso.Server/Services/SoapDefinitions/legacy-soap-operations.json").read_text(encoding="utf-8-sig"))
    rows = []
    for item in catalog:
        method = find_method(source, item["Name"])
        if method["parameters"] != [p["Name"] for p in item["Parameters"]]:
            raise ValueError("參數與目前說明契約不同：" + item["Name"])
        body = method["body"]
        helper_names = sorted(set(re.findall(r"\b(IsSSO\w*|IsAuthenticated)\s*\(", body)))
        helper_rows = []
        for name in helper_names:
            helper = find_method(helpers, name)
            helper_rows.append({"method": name, "line": helper["line"], **dependencies(helper["body"])})
        rows.append({
            "method": item["Name"], "title": item["Title"], "implemented": item["Implemented"],
            "sourceLine": method["line"], "returnType": method["returnType"],
            "parameters": method["parameters"], "namespace": item["Namespace"], "action": item["Action"],
            "legacyServiceIds": sorted(set(re.findall(r'["\x27](WS\d+)["\x27]', body))),
            **dependencies(body), "directoryHelpers": helper_rows,
            "dataSetColumns": list(dict.fromkeys(re.findall(r'Columns\.Add\(\s*"([^"]+)"', body))),
            "callsLegacyPasswordLogger": "WebServiceAddLogManager(" in body,
            "referencesPasswordOutput": bool(re.search(r'["\x27](?:pwd|coded_usr_pwd)["\x27]', body)),
        })
    return {
        "source": str(source_file), "sourceSha256": hashlib.sha256(source_file.read_bytes()).hexdigest(),
        "helperSource": str(helper_file), "helperSha256": hashlib.sha256(helper_file.read_bytes()).hexdigest(),
        "methodCount": len(rows), "implementedCount": sum(r["implemented"] for r in rows), "methods": rows,
    }


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--legacy", type=Path, required=True, help="包含 asmx.cs 的舊 SOA 目錄（唯讀）")
    parser.add_argument("--output", type=Path, default=ROOT / ".local/legacy-soap-migration/inventory.json")
    args = parser.parse_args()
    report = inventory(args.legacy)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"Checked {report['methodCount']} method signatures; {report['implementedCount']} implemented.")
    print("Inventory:", args.output)


if __name__ == "__main__":
    main()
