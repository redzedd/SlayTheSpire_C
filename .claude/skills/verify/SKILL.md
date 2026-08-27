---
name: verify
description: SlaytheSpire_C 的驗證管道——任何程式碼變更後、宣稱「完成」前必跑。編譯檢查(必跑) → EditMode 測試(邏輯變更) → play 煙霧(行為/場景變更) → log 掃描。全部管道 2026-08-03 實測通過;含 unity-mcp RunCommand 的怪癖與正確寫法。
---

# SlaytheSpire_C Verify(2026-08-03 實測建立)

**鐵律:沒跑第 1 項(編譯檢查)不准說「完成」。** 證據分級回報:[驗證] 跑過看過 / [推論] 讀碼推出 / [假設] 未驗證;失敗如實回報附輸出原文。

## 前置:MCP 現實(實測結論,勿浪費時間試錯)

- **用 `unity-mcp`(MCP for Unity)工具**,已驗證可對本專案通訊。
- **`coplay-mcp` 的橋接沒裝在本專案**——工具一律回 "Unity Editor is not running at the specified project root",不要重試、不要 set_unity_project_root(沒用,2026-08-03 已試)。
- Editor 沒開時所有 MCP 管道死亡 → 結論標 **UNVERIFIED + 請使用者開 editor**,不准降級成「應該沒問題」。

## Unity_RunCommand 怪癖(2026-08-03 付過代價)

1. 類別**必須**叫 `CommandScript`、**必須** `internal`,實作 `IRunCommand`。
2. **禁止巢狀類別**——前處理器會把巢狀類別複製一份到 namespace 層級,產生 CS1527。需要輔助類別(如 ICallbacks 實作)就寫成**並列的頂層 internal class**。
3. 程式碼會被自動包進 namespace,不要自己寫 namespace。
4. 執行是同步回傳,但你啟動的非同步工作(如 TestRunnerApi)在回傳後才完成 → 結果要靠落檔/console 標記回收。
5. **程式碼含 `File.Delete` 會整包被拒**,錯誤訊息是誤導性的「User interactions are not supported for MCP tool calls」——動態執行安全層把刪檔當需確認操作。清舊檔改在 MCP 之外用 shell 做(`rm -f`),或用 `File.WriteAllText` 覆寫。診斷法:最小探針能跑、完整腳本不能跑 → 二分找出觸發 API。

## 管道 1:編譯檢查(任何程式碼變更後必跑)

1. 建/改腳本用 `Unity_CreateScript` / `Unity_ScriptApplyEdits`(自動觸發匯入+編譯);用 Write 工具寫的檔案(如 .asmdef)要補一發 RunCommand:`AssetDatabase.Refresh();`。
2. 確認編譯完成且產物存在(RunCommand):

```csharp
using UnityEditor;
using System.IO;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        result.Log("isCompiling={0}, isUpdating={1}", EditorApplication.isCompiling, EditorApplication.isUpdating);
        result.Log("STS.Core.dll={0}", File.Exists("Library/ScriptAssemblies/STS.Core.dll"));
        result.Log("STS.Core.Tests.dll={0}", File.Exists("Library/ScriptAssemblies/STS.Core.Tests.dll"));
    }
}
```

3. `Unity_ReadConsole`(Types=["Error"])→ 必須零筆。isCompiling=True 就等一輪再查,不要在編譯中下結論。

## 管道 2:EditMode 測試(邏輯變更後必跑;改到 STS.Core 一律跑)

RunCommand 啟動(注意:ResultWriter 是頂層類別,不是巢狀):

```csharp
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using System.IO;
using System.Text;

internal class StsTestResultWriter : ICallbacks
{
    public void RunStarted(ITestAdaptor testsToRun) { }
    public void RunFinished(ITestResultAdaptor r)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Format("STS_TEST_RESULT: {0} | pass={1} fail={2} skip={3} | duration={4:F2}s",
            r.TestStatus, r.PassCount, r.FailCount, r.SkipCount, r.Duration));
        File.WriteAllText("Temp/STS_TestResult.txt", sb.ToString());
        Debug.Log(sb.ToString());
    }
    public void TestStarted(ITestAdaptor test) { }
    public void TestFinished(ITestResultAdaptor r)
    {
        if (r.TestStatus == TestStatus.Failed && !r.HasChildren)
            Debug.LogError("STS_TEST_FAIL: " + r.FullName + " :: " + r.Message);
    }
}

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        var api = ScriptableObject.CreateInstance<TestRunnerApi>();
        api.hideFlags = HideFlags.HideAndDontSave;
        api.RegisterCallbacks(new StsTestResultWriter());
        var filter = new Filter { testMode = TestMode.EditMode, assemblyNames = new[] { "STS.Core.Tests", "STS.Content.Tests" } };
        api.Execute(new ExecutionSettings(filter));
        result.Log("EditMode 測試已啟動");
    }
}
```

回收結果:輪詢 `Temp/STS_TestResult.txt`(bash,約 2 秒間隔,30 秒上限),必須看到 `Passed | pass=N fail=0`;失敗時讀 `Temp/STS_TestFails.txt` 或 console 掃 `STS_TEST_FAIL`。基線:2026-08-27 M5 後為 102 tests(引擎/地圖 77+內容/Run 25)/約 1.3s。改到 STS.Core 或 STS.Data 或 `Assets/Data/Source/*.json` 都必跑;改了 JSON 記得先跑匯入器選單 `STS/重新匯入資料(JSON→SO)`。兩個已付代價的回呼陷阱:(a) 編輯器重啟會丟掉進行中的測試回呼;(b) **同一個 RunCommand 裡 Refresh+啟動測試,domain reload 會把回呼吃掉**——正確做法:先 Refresh 等編譯完,再用「另一個」RunCommand 單獨啟動測試。結果檔 30 秒不出現就重新啟動測試,不要空等。

## 管道 3:play 煙霧(行為/場景變更後跑;M4 起有具體流程)

1. 確認 `Assets/Scenes/Main.unity` 是 active scene(不確定就 RunCommand 查 `SceneManager.GetActiveScene().path`)。
2. `Unity_ManageEditor` Action=Play(WaitForCompletion=true),然後 **shell sleep 3-4 秒**等開場播放結束(播放中輸入鎖定,煙霧會回「輸入鎖定中」)。
3. RunCommand:`Object.FindFirstObjectByType<STS.Game.GameController>()` → `game.Combat.煙霧_出第一張可出的牌()`——走與拖曳出牌相同的指令路徑,回傳字串要見「已出牌」。
4. `Unity_ReadConsole`(Types=["Error"])掃 Exception → 必須零筆。
5. 視覺留證(可選):RunCommand `ScreenCapture.CaptureScreenshot("Temp/xxx.png")`(Overlay UI 不入相機,Camera_Capture 照不到,必須用 ScreenCapture)。
6. `Unity_ManageEditor` Action=Stop。**不要留在 play mode 收工。**

## 管道 4:效能哨兵(之後有戰鬥場景才啟用)

尚無基準。建立真實戰鬥場景後,用全域 `/unity-frame-spike` 流程與 unity-mcp Profiler 工具訂基準,再回填本節。

## 回報格式

```
結論:PASS / FAIL / UNVERIFIED(原因)
1. 編譯:[驗證] isCompiling=False、console 零 Error
2. 測試:[驗證] STS_TEST_RESULT: Passed | pass=N fail=0(或:未跑,原因)
3. 煙霧:[驗證] Play 進出正常、零 Exception(或:未跑,原因)
未覆蓋:<明說>
```
