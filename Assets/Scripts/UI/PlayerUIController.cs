using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace CineScape
{
    public class SeekDrag : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IEndDragHandler
    {
        public bool Dragging { get; private set; }
        public Action<float> Commit;
        public void OnPointerDown(PointerEventData e) { Dragging = true; }
        public void OnPointerUp(PointerEventData e) { Finish(); }
        public void OnEndDrag(PointerEventData e) { Finish(); }
        void Finish() { if(!Dragging) return; Dragging=false; Commit?.Invoke(GetComponent<Slider>().value); }
    }

    public class PlayerUIController : MonoBehaviour
    {
        VideoPlaybackController video;
        CameraDistanceController distance;
        SettingsManager settings;
        CanvasGroup controls;
        RectTransform settingsPanel;
        RectTransform correctionPanel;
        Slider correction;
        Text correctionLabel;
        Slider seek, volume, proximity;
        SeekDrag seekDrag;
        Text clock, distanceLabel, status, playLabel, muteLabel, qualityLabel;
        Font font;
        Vector3 mouse;
        float lastActivity;
        bool dialogOpen;
        string notice;
        float noticeUntil;
        readonly Color panelColor = new Color(.035f,.045f,.065f,.96f);
        public void Initialize(VideoPlaybackController v, CameraDistanceController d, SettingsManager s)
        {
            video=v; distance=d; settings=s;
            // Windows 自带中文字体，不附带字体文件；Unity UI 使用动态字体。
            font = Font.CreateDynamicFontFromOSFont(new[]{"Microsoft YaHei UI","Microsoft YaHei","SimHei","Arial"},20);
            var canvasObject = new GameObject("中文播放器界面", typeof(RectTransform), typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform,false);
            canvasObject.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
            var scaler=canvasObject.GetComponent<CanvasScaler>(); scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1920,1080); scaler.matchWidthOrHeight=.5f;
            if(FindFirstObjectByType<EventSystem>() == null) new GameObject("UI 事件系统",typeof(EventSystem),typeof(StandaloneInputModule));
            var bar=Panel(canvasObject.transform,"控制栏",new Vector2(0,0),new Vector2(1,0),new Vector2(0,0),new Vector2(0,164));
            controls=bar.gameObject.AddComponent<CanvasGroup>();
            var row=Row(bar,"播放控制",new Vector2(0,80),new Vector2(0,70));
            Button(row,"打开视频",Open);
            playLabel=Button(row,"播放",()=>video.Toggle()).GetComponentInChildren<Text>();
            Button(row,"停止",()=>video.Stop());
            clock=Label(row,"00:00 / 00:00",235);
            seek=Slider(row,0,1,0,540); seekDrag=seek.gameObject.AddComponent<SeekDrag>(); seekDrag.Commit=t=>video.Seek(t*video.Duration);
            // 只在鼠标释放时提交；键盘跳转由 Left/Right 处理，避免拖动每帧解码。
            muteLabel=Button(row,"静音",()=>{ video.Muted=!video.Muted; },82).GetComponentInChildren<Text>();
            volume=Slider(row,0,1,video.Volume,140); volume.onValueChanged.AddListener(t=>video.Volume=t);
            Button(row,"全屏",ToggleFullscreen,88);
            Button(row,"设置",()=>{ settingsPanel.gameObject.SetActive(!settingsPanel.gameObject.activeSelf);correctionPanel.gameObject.SetActive(false); },88);
            var second=Row(bar,"距离控制",new Vector2(0,10),new Vector2(0,65));
            distanceLabel=Label(second,"观看距离 50%",190);
            Label(second,"远",35); proximity=Slider(second,0,1,distance.Value,430); proximity.onValueChanged.AddListener(distance.Set); Label(second,"近",35);
            Button(second,"恢复默认距离",()=>distance.Set(settings.Data.defaultDistance),170);
            Button(second,"银幕校正",()=>{correctionPanel.gameObject.SetActive(!correctionPanel.gameObject.activeSelf);settingsPanel.gameObject.SetActive(false);},130);
            Label(second,"滚轮：远近    空格：播放/暂停    ← →：跳转    F：全屏",650);
            status=Label(canvasObject.transform,"",1000);
            var sr=status.rectTransform; sr.anchorMin=sr.anchorMax=new Vector2(.5f,.85f); sr.pivot=new Vector2(.5f,.5f);
            sr.sizeDelta=new Vector2(1200,140); status.alignment=TextAnchor.MiddleCenter;
            settingsPanel=Panel(canvasObject.transform,"设置面板",new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2(-380,-360),new Vector2(380,360));
            var layout=settingsPanel.gameObject.AddComponent<VerticalLayoutGroup>(); layout.padding=new RectOffset(30,30,20,20); layout.spacing=14;
            layout.childControlHeight=true; layout.childControlWidth=true; layout.childForceExpandHeight=false;
            Label(settingsPanel,"设置 · CineScape 3D",600);
            Label(settingsPanel,"界面语言：简体中文（预留英语 / 日本語）",600);
            var qr=SimpleRow(settingsPanel); qualityLabel=Label(qr,"画质：中",120);
            foreach(var level in new[]{0,1,2})
            {
                int selected=level;
                Button(qr,new[]{"低","中","高"}[level],()=>{settings.Data.quality=selected; QualitySettingsController.Apply(selected);settings.Save();},110);
            }
            Toggle(settingsPanel,"启动时全屏",settings.Data.fullscreen,t=>{settings.Data.fullscreen=t;settings.Save();});
            Toggle(settingsPanel,"记住上次观看距离",settings.Data.rememberDistance,t=>{settings.Data.rememberDistance=t;settings.Save();});
            Toggle(settingsPanel,"3 秒后自动隐藏控制栏",settings.Data.autoHide,t=>{settings.Data.autoHide=t;settings.Save();});
            Label(settingsPanel,"默认音量（也应用到当前播放）",600);
            var dv=Slider(settingsPanel,0,1,settings.Data.defaultVolume,600);
            dv.onValueChanged.AddListener(t=>{settings.Data.defaultVolume=t;video.Volume=t;settings.Save();});
            Label(settingsPanel,"默认观看距离（0% 最远 / 50% 初始 / 100% 最近）",650);
            var dd=Slider(settingsPanel,0,1,settings.Data.defaultDistance,600);
            dd.onValueChanged.AddListener(t=>{settings.Data.defaultDistance=t;settings.Save();});
            Button(settingsPanel,"关闭设置",()=>settingsPanel.gameObject.SetActive(false),600);
            settingsPanel.gameObject.SetActive(false); mouse=Input.mousePosition; lastActivity=UnityEngine.Time.unscaledTime;
            correctionPanel=Panel(canvasObject.transform,"银幕校正面板",new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2(-380,-180),new Vector2(380,180));
            var correctionLayout=correctionPanel.gameObject.AddComponent<VerticalLayoutGroup>();
            correctionLayout.padding=new RectOffset(30,30,20,20);correctionLayout.spacing=12;
            correctionLayout.childControlHeight=true;correctionLayout.childControlWidth=true;correctionLayout.childForceExpandHeight=false;
            Label(correctionPanel,"银幕校正 · 像投影仪一样调整",650);
            correctionLabel=Label(correctionPanel,"",650);
            correction=Slider(correctionPanel,0,1,distance.Keystone,650);
            correction.onValueChanged.AddListener(t=>{distance.SetKeystone(t);settings.Data.keystone=t;settings.Save();});
            Label(correctionPanel,"0% 保留透视；100% 自动校正为矩形，随观看距离更新。",690);
            var buttons=SimpleRow(correctionPanel);
            Button(buttons,"自动校正",()=>correction.value=1,180);
            Button(buttons,"关闭校正",()=>correction.value=0,180);
            Button(buttons,"关闭",()=>correctionPanel.gameObject.SetActive(false),180);
            correctionPanel.gameObject.SetActive(false);
            if(!string.IsNullOrEmpty(settings.LastError)) Notify(settings.LastError);
        }
        RectTransform Rect(Transform parent,string name)
        {
            var o=new GameObject(name,typeof(RectTransform)); o.transform.SetParent(parent,false); return o.GetComponent<RectTransform>();
        }
        RectTransform Panel(Transform parent,string name,Vector2 a,Vector2 b,Vector2 min,Vector2 max)
        {
            var r=Rect(parent,name); r.anchorMin=a;r.anchorMax=b;r.offsetMin=min;r.offsetMax=max;
            r.gameObject.AddComponent<Image>().color=panelColor; return r;
        }
        Transform Row(Transform parent,string name,Vector2 min,Vector2 max)
        {
            var r=Rect(parent,name);r.anchorMin=new Vector2(0,0);r.anchorMax=new Vector2(1,0);r.offsetMin=min;r.offsetMax=min+max;
            var g=r.gameObject.AddComponent<HorizontalLayoutGroup>();g.padding=new RectOffset(24,24,5,5);g.spacing=12;
            g.childControlWidth=false;g.childControlHeight=true;g.childForceExpandWidth=false;g.childForceExpandHeight=true;return r;
        }
        Transform SimpleRow(Transform parent)
        {
            var r=Rect(parent,"设置行");Size(r,650,48);
            var g=r.gameObject.AddComponent<HorizontalLayoutGroup>();g.spacing=12;g.childControlWidth=false;g.childForceExpandWidth=false;return r;
        }
        void Size(RectTransform r,float width,float height=48)
        {
            r.sizeDelta=new Vector2(width,height);var l=r.gameObject.AddComponent<LayoutElement>();l.preferredWidth=width;l.preferredHeight=height;l.minHeight=height;
        }
        Text Label(Transform parent,string text,float width)
        {
            var r=Rect(parent,"文字");Size(r,width);var t=r.gameObject.AddComponent<Text>();t.font=font;t.fontSize=20;t.color=new Color(.9f,.92f,.96f);
            t.text=text;t.alignment=TextAnchor.MiddleLeft;t.raycastTarget=false;return t;
        }
        Button Button(Transform parent,string text,UnityEngine.Events.UnityAction action,float width=120)
        {
            var r=Rect(parent,text);Size(r,width);var img=r.gameObject.AddComponent<Image>();img.color=new Color(.15f,.19f,.26f);
            var b=r.gameObject.AddComponent<Button>();b.targetGraphic=img;var c=b.colors;c.highlightedColor=new Color(.48f,.65f,.85f);c.pressedColor=new Color(.25f,.44f,.65f);b.colors=c;
            var t=Label(r,text,width);t.rectTransform.anchorMin=Vector2.zero;t.rectTransform.anchorMax=Vector2.one;t.rectTransform.offsetMin=Vector2.zero;t.rectTransform.offsetMax=Vector2.zero;t.alignment=TextAnchor.MiddleCenter;
            b.onClick.AddListener(action); DisableNavigation(b);return b;
        }
        Slider Slider(Transform parent,float min,float max,float value,float width)
        {
            var r=Rect(parent,"滑块");Size(r,width);var slider=r.gameObject.AddComponent<Slider>();
            var track=Rect(r,"轨道");track.anchorMin=new Vector2(0,.43f);track.anchorMax=new Vector2(1,.57f);track.offsetMin=Vector2.zero;track.offsetMax=Vector2.zero;track.gameObject.AddComponent<Image>().color=new Color(.23f,.29f,.38f);
            var handle=Rect(r,"滑块柄");handle.sizeDelta=new Vector2(18,30);var graphic=handle.gameObject.AddComponent<Image>();graphic.color=new Color(.65f,.79f,.97f);
            slider.handleRect=handle;slider.targetGraphic=graphic;slider.minValue=min;slider.maxValue=max;slider.value=value;
            DisableNavigation(slider);return slider;
        }
        void Toggle(Transform parent,string label,bool value,UnityEngine.Events.UnityAction<bool> action)
        {
            var r=Rect(parent,label);Size(r,650);var toggle=r.gameObject.AddComponent<Toggle>();
            var bg=Rect(r,"复选框");bg.anchorMin=bg.anchorMax=new Vector2(0,.5f);bg.anchoredPosition=new Vector2(15,0);bg.sizeDelta=new Vector2(28,28);
            var image=bg.gameObject.AddComponent<Image>();image.color=new Color(.23f,.3f,.4f);
            var check=Rect(bg,"选中");check.sizeDelta=new Vector2(18,18);var mark=check.gameObject.AddComponent<Image>();mark.color=new Color(.6f,.85f,1);
            toggle.targetGraphic=image;toggle.graphic=mark;toggle.isOn=value;
            var t=Label(r,label,600);t.rectTransform.anchoredPosition=new Vector2(340,0);
            toggle.onValueChanged.AddListener(action);DisableNavigation(toggle);
        }
        void DisableNavigation(Selectable s) { s.navigation=new Navigation{mode=Navigation.Mode.None}; }
        void Open()
        {
            if(dialogOpen) return;
            dialogOpen=true;
            try { video.Open(WindowsFileDialog.Open()); }
            catch(Exception e) { Debug.LogWarning(e); Notify("打开文件失败："+e.Message); }
            finally { dialogOpen=false;lastActivity=UnityEngine.Time.unscaledTime; }
        }
        void ToggleFullscreen() { Screen.fullScreenMode=FullScreenMode.FullScreenWindow;Screen.fullScreen=!Screen.fullScreen; }
        void Notify(string message) { notice=message;noticeUntil=UnityEngine.Time.unscaledTime+8; }
        bool Editing()
        {
            var current=EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            return current != null && current.GetComponent<InputField>() != null;
        }
        void Update()
        {
            if(video==null) return;
            bool activity=mouse!=Input.mousePosition || Input.anyKey || Mathf.Abs(Input.mouseScrollDelta.y)>0;
            mouse=Input.mousePosition;if(activity) lastActivity=UnityEngine.Time.unscaledTime;
            bool overUI=EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            bool show=!settings.Data.autoHide || !video.Playing || settingsPanel.gameObject.activeSelf || correctionPanel.gameObject.activeSelf || seekDrag.Dragging || overUI || UnityEngine.Time.unscaledTime-lastActivity<3;
            controls.alpha=show?1:0;controls.interactable=show;controls.blocksRaycasts=show;
            Cursor.visible=show;
            if(!Editing() && !dialogOpen && !settingsPanel.gameObject.activeSelf && !correctionPanel.gameObject.activeSelf)
            {
                if(Input.GetKeyDown(KeyCode.Space)) video.Toggle();
                if((Input.GetKey(KeyCode.LeftControl)||Input.GetKey(KeyCode.RightControl))&&Input.GetKeyDown(KeyCode.O)) Open();
                if(Input.GetKeyDown(KeyCode.LeftArrow)) video.Seek(video.Time-5);
                if(Input.GetKeyDown(KeyCode.RightArrow)) video.Seek(video.Time+5);
                if(Input.GetKeyDown(KeyCode.UpArrow)) video.Volume+=.05f;
                if(Input.GetKeyDown(KeyCode.DownArrow)) video.Volume-=.05f;
                if(Input.GetKeyDown(KeyCode.F)) ToggleFullscreen();
                if(Input.GetKeyDown(KeyCode.Home)) distance.Set(settings.Data.defaultDistance);
                if(!overUI) distance.Set(distance.Value+Input.mouseScrollDelta.y*.035f);
            }
            if(Input.GetKeyDown(KeyCode.Escape) && !Editing())
            { if(correctionPanel.gameObject.activeSelf)correctionPanel.gameObject.SetActive(false);else if(settingsPanel.gameObject.activeSelf)settingsPanel.gameObject.SetActive(false); else Screen.fullScreen=false; }
            correctionLabel.text=$"梯形校正 {distance.Keystone*100:0}%";
            proximity.SetValueWithoutNotify(distance.Value);volume.SetValueWithoutNotify(video.Volume);
            distanceLabel.text=$"观看距离 {distance.Value*100:0}%";
            if(!seekDrag.Dragging) seek.SetValueWithoutNotify(video.Duration>0?(float)(video.Time/video.Duration):0);
            seek.interactable=video.Ready && video.Duration>0;
            clock.text=Format(video.Time)+" / "+Format(video.Duration);
            playLabel.text=video.Playing?"暂停":"播放";
            muteLabel.text=video.Muted?"取消静音":"静音";
            qualityLabel.text="画质："+new[]{"低","中","高"}[settings.Data.quality];
            if(!dialogOpen) status.text=UnityEngine.Time.unscaledTime<noticeUntil?notice:video.Status;
        }
        public static string Format(double seconds)
        {
            if(double.IsNaN(seconds)||double.IsInfinity(seconds)||seconds<0) seconds=0;
            var t=TimeSpan.FromSeconds(seconds);return t.TotalHours>=1?$"{(int)t.TotalHours:00}:{t.Minutes:00}:{t.Seconds:00}":$"{t.Minutes:00}:{t.Seconds:00}";
        }
        void OnDestroy() { Cursor.visible=true;if(font!=null)Destroy(font); }
    }
}
