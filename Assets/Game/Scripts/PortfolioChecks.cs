#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PortfolioChecks : MonoBehaviour
{
    bool failed,finished;
    float deadline;
    int assertions;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Begin()
    {
        if(!Environment.GetCommandLineArgs().Contains("--portfolio-checks")) return;
        var go=new GameObject("PortfolioChecks");DontDestroyOnLoad(go);go.AddComponent<PortfolioChecks>();
    }
    void Awake(){ deadline=Time.realtimeSinceStartup+420;Application.logMessageReceived+=OnLog; }
    void OnDestroy(){Application.logMessageReceived-=OnLog;}
    void OnLog(string message,string stack,LogType type){ if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)failed=true; }
    void Update(){if(!finished&&(failed||Time.realtimeSinceStartup>deadline)) Finish(false);}
    void Check(bool ok,string label){assertions++;if(!ok)throw new Exception("CHECK FAILED: "+label);}
    static object Read(object target,string name)=>target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public).GetValue(target);
    static void Call(object target,string name,params object[] args)=>target.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public).Invoke(target,args);
    IEnumerator Start()
    {
        yield return new WaitForSecondsRealtime(1);
        var config=PortfolioConfig.Load();
        Check(SceneManager.GetActiveScene().name=="ModeSelect","entry menu");
        Check(FindObjectsByType<ModeNavigation>(FindObjectsSortMode.None).Length==config.modes.Length,"all mode links");
        Capture("menu.png");
        foreach(var mode in config.modes)
        {
            FindObjectsByType<ModeNavigation>(FindObjectsSortMode.None).Single(n=>n.targetScene==mode.scene).GetComponent<Button>().onClick.Invoke();
            yield return new WaitForSecondsRealtime(.5f);
            Check(SceneManager.GetActiveScene().name==mode.scene,"mode navigation");
            var stem=FindFirstObjectByType<StemGameManager>();
            Check(stem!=null && stem.yonergeText.text.Length>0,"initialized instruction");
#if SEQUENCE
            var game=stem.GetComponent<InputSequential>();
            Check(game.inputFields.Count>0,"editable fields exist");
            Capture(mode.scene+"-level1.png");
            var field=game.inputFields[0];field.text="016";field.onEndEdit.Invoke(field.text);yield return null;
            Check(field.text==""&&!field.readOnly,"reject noncanonical integer");
            yield return new WaitForSecondsRealtime(.1f);
            Check(stem.gamedata.Data[0].Wrong==1,"wrong input recorded");
            field.text="999999";field.onEndEdit.Invoke(field.text);yield return null;
            Check(field.text==""&&!field.readOnly,"reject out-of-range answer");
            for(int round=0;round<3;round++)
            {
                int before=game.levelIndex;
                int first=int.Parse(game.objectParent.transform.GetChild(0).GetComponentInChildren<TMP_Text>().text);
                int last=int.Parse(game.objectParent.transform.GetChild(game.objectParent.transform.childCount-1).GetComponentInChildren<TMP_Text>().text);
                int delta=(last-first)/(game.objectParent.transform.childCount-1);
                foreach(var f in game.inputFields.ToArray())
                {
                    int value=first+f.transform.GetSiblingIndex()*delta;
                    f.text=value.ToString();f.onEndEdit.Invoke(f.text);
                    Check(f.readOnly,"correct input locks field");
                    f.onEndEdit.Invoke(f.text); // End-edit can be delivered again when focus changes.
                }
                yield return new WaitForSecondsRealtime(1.5f);
                Check(game.levelIndex==before+1&&stem.levelIndex==game.levelIndex,"single level advance and HUD sync");
                if(round==0)Capture(mode.scene+"-level2.png");
            }
#elif CARDS
            var game=stem.GetComponent<CardMatchGame>();
            var map=(IDictionary)Read(game,"cardMap");
            Check(map.Count==game.pairsCount*2&&game.pairsCount>0,"complete deck");
            var entries=map.Values.Cast<object>().ToArray();
            var a=entries[0];var b=entries.First(e=>(int)Read(e,"value")!=(int)Read(a,"value"));
            ((GameObject)Read(a,"root")).GetComponent<Button>().onClick.Invoke();
            ((GameObject)Read(b,"root")).GetComponent<Button>().onClick.Invoke();
            yield return new WaitForSecondsRealtime(.2f);
            Capture(mode.scene+"-level1.png");
            yield return new WaitForSecondsRealtime(game.revealDuration + .1f);
            Check(!((GameObject)Read(a,"emptyObject")).activeSelf&&!((GameObject)Read(b,"emptyObject")).activeSelf,"mismatch hides faces");
            Check(stem.gamedata.Data[0].Wrong==1,"mismatch counted");
            for(int round=0;round<2;round++)
            {
                map=(IDictionary)Read(game,"cardMap");
                var groups=map.Values.Cast<object>().GroupBy(e=>(int)Read(e,"value")).ToArray();
                Check(groups.Length==game.pairsCount&&groups.All(g=>g.Count()==2),"exactly two cards per answer");
                foreach(var group in groups)
                {
                    foreach(var e in group){var button=((GameObject)Read(e,"root")).GetComponent<Button>();button.onClick.Invoke();button.onClick.Invoke();}
                    yield return null;
                }
                yield return new WaitForSecondsRealtime(1.5f);
                Check(game.levelIndex==round+1&&stem.levelIndex==game.levelIndex,"deck completion advances once");
                if(round==0){map=(IDictionary)Read(game,"cardMap");var pair=map.Values.Cast<object>().GroupBy(e=>(int)Read(e,"value")).First();foreach(var e in pair)((GameObject)Read(e,"root")).GetComponent<Button>().onClick.Invoke();yield return null;Capture(mode.scene+"-level2.png");}
                // Reload the second-level deck after the gallery pair to solve it from a clean board.
                if(round==0)Call(game,"SetupGame");
            }
#elif ODDEVEN
            var game=stem.GetComponent<OddOrEvenGame>();
            Time.timeScale=5;yield return new WaitForSecondsRealtime(1.4f);Time.timeScale=1;
            Capture(mode.scene+"-level1.png");
            for(int round=0;round<2;round++)
            {
                for(int attempt=0;attempt<150 && game.levelIndex==round;attempt++)
                {
                    Call(game,"SpawnObject");
                    var available=game.objectParent.GetComponentsInChildren<OddOrEvenInfo>().Where(i=>!i.isCaught).ToArray();
                    foreach(var item in available)
                    {
                        if(game.levelIndex!=round)break;
                        if(attempt>0&&item.numberType!=game.targetType)continue;
                        var button=item.GetComponent<Button>();int before=(int)Read(game,"remaining");
                        bool good=item.numberType==game.targetType;
                        button.onClick.Invoke();button.onClick.Invoke();
                        Check((int)Read(game,"remaining")==before-(good?1:0),"parity and double-click protection");
                        yield return new WaitForSecondsRealtime(.04f);
                        if((bool)Read(game,"levelTransitioning"))break;
                    }
                    if((bool)Read(game,"levelTransitioning"))yield return new WaitForSecondsRealtime(1.5f);
                }
                Check(game.levelIndex==round+1&&stem.levelIndex==game.levelIndex,"parity level transition");
                if(round==0){Time.timeScale=5;yield return new WaitForSecondsRealtime(1.4f);Time.timeScale=1;Capture(mode.scene+"-level2.png");}
            }
#elif BUBBLE
            var game=stem.GetComponent<BubblePopMathGame>();
            Time.timeScale=5;yield return new WaitForSecondsRealtime(1.5f);Time.timeScale=1;
            Capture(mode.scene+"-level1.png");
            bool checkedWrong=false;
            for(int round=0;round<2;round++)
            {
                for(int attempt=0;attempt<350&&game.levelIndex==round;attempt++)
                {
                    if((bool)Read(game,"levelTransitioning")){yield return new WaitForSecondsRealtime(1.4f);continue;}
                    Call(game,"SpawnBubble");
                    var bubbles=((IList)Read(game,"activeBubbles")).Cast<GameObject>().Where(g=>g.GetComponent<Button>().interactable).ToArray();
                    foreach(var bubble in bubbles)
                    {
                        if((bool)Read(game,"levelTransitioning"))break;
                        int value=int.Parse(bubble.GetComponentInChildren<TMP_Text>().text);
                        int target=(int)Read(game,"currentTargetNumber"),sum=(int)Read(game,"currentSum");
                        int active=mode.isRandom!=0?(int)(BubbleGameMode)Read(game,"currentRandomMode"):(int)game.gameMode;
                        bool good=active==0?sum+value<=target:active==1?value%target==0:active==2?target%value==0:active==3?Prime(value):active==4?value>target:value<target;
                        if(!good&&checkedWrong)continue;
                        int pops=game.correctPops,wrong=stem.gamedata.Data[stem.currentPage].Wrong;
                        var button=bubble.GetComponent<Button>();button.onClick.Invoke();button.onClick.Invoke();
                        yield return new WaitForSecondsRealtime(.04f);
                        if(!good){Check(stem.gamedata.Data[stem.currentPage].Wrong==wrong+1,"wrong bubble counted once");checkedWrong=true;}
                        else if(active!=0)Check(game.correctPops==pops+1,"correct bubble counted once");
                    }
                    // Allow unused candidates to leave the playfield while searching for a valid choice.
                    Time.timeScale=8;yield return new WaitForSecondsRealtime(.04f);Time.timeScale=1;
                }
                Check(game.levelIndex==round+1&&stem.levelIndex==game.levelIndex,"bubble level progression");
                if(round==0){Time.timeScale=5;yield return new WaitForSecondsRealtime(1.5f);Time.timeScale=1;Capture(mode.scene+"-level2.png");}
            }
#endif
            GameObject.Find("Restart").GetComponent<Button>().onClick.Invoke();yield return new WaitForSecondsRealtime(.5f);
            Check(FindFirstObjectByType<StemGameManager>().levelIndex==0,"restart resets level");
            GameObject.Find("Modes").GetComponent<Button>().onClick.Invoke();yield return new WaitForSecondsRealtime(.5f);
            Check(SceneManager.GetActiveScene().name=="ModeSelect","menu return");
            Debug.Log("MODE_OK "+mode.scene);
        }
        Debug.Log("ASSERTIONS "+assertions);Finish(!failed);
    }
    static bool Prime(int v){if(v<2)return false;for(int d=2;d*d<=v;d++)if(v%d==0)return false;return true;}
    static void Capture(string name)
    {
        var camera=Camera.main;
        var canvases=FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c=>c.isRootCanvas).ToArray();
        foreach(var c in canvases){c.renderMode=RenderMode.ScreenSpaceCamera;c.worldCamera=camera;c.planeDistance=1;}
        Canvas.ForceUpdateCanvases();
        var rt=new RenderTexture(1440,900,24);camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
        var image=new Texture2D(1440,900,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1440,900),0,0);image.Apply();
        string dir=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../Docs"));Directory.CreateDirectory(dir);File.WriteAllBytes(Path.Combine(dir,name),image.EncodeToPNG());
        camera.targetTexture=null;RenderTexture.active=null;Destroy(rt);Destroy(image);
        foreach(var c in canvases)c.renderMode=RenderMode.ScreenSpaceOverlay;
    }
    void Finish(bool success){finished=true;Time.timeScale=1;Debug.Log(success?"PORTFOLIO_CHECKS_OK":"PORTFOLIO_CHECKS_FAILED");Application.Quit(success?0:1);}
}
#endif

