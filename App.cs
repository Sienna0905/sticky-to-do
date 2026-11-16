using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Xml.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

namespace SideNote {
 public class TaskItem {
  public string Id = Guid.NewGuid().ToString();
  public string Title = "";
  public string Section = "未分类";
  public string Start = "";
  public string End = "";
  public string Reminder = "";
  public string Notified = "";
  public int Priority;
  public string Color = "#3B7200";
  public bool Done;
  public string Completed = "";
 }
 public class Note {
  public string Id = Guid.NewGuid().ToString();
  public string Title = "未命名笔记";
  public string Folder = "随手记";
  public string Rtf = "";
  public DateTime Updated = DateTime.Now;
 }
 public class State {
  public List<TaskItem> Tasks = new List<TaskItem>();
  public List<Note> Notes = new List<Note>();
  public List<string> Sections = new List<string>{"未分类"};
  public List<string> Folders = new List<string>{"随手记"};
  public double Opacity = .97;
  public string Font = "Microsoft YaHei UI";
  public double PanelWidth = 350;
  public double PanelHeight;
 }
 public static class Storage {
  public static string Dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SideNote");
  public static string FilePath { get { return Path.Combine(Dir,"data.xml"); } }
  public static State Load() {
   if (!File.Exists(FilePath)) return new State();
   try { using(var f=File.OpenRead(FilePath)) return Normalize((State)new XmlSerializer(typeof(State)).Deserialize(f)); }
   catch { if(File.Exists(FilePath+".bak")) { using(var f=File.OpenRead(FilePath+".bak")) return Normalize((State)new XmlSerializer(typeof(State)).Deserialize(f)); } throw; }
  }
  public static State Normalize(State s) {
   s.Sections=s.Sections.Select(x=>x=="收件箱"?"未分类":x).Distinct().ToList();
   foreach(var task in s.Tasks) if(task.Section=="收件箱")task.Section="未分类";
   return s;
  }
  public static void Save(State s) {
   Directory.CreateDirectory(Dir);
   using(var f=File.Create(FilePath+".tmp")) new XmlSerializer(typeof(State)).Serialize(f,s);
   if(File.Exists(FilePath)) File.Replace(FilePath+".tmp",FilePath,FilePath+".bak"); else File.Move(FilePath+".tmp",FilePath);
  }
  public static bool Today(TaskItem t, DateTime today) {
   if(t.Done) return false;
   DateTime a,b; bool start=DateTime.TryParse(t.Start,out a), end=DateTime.TryParse(t.End,out b);
   if(start && a.Date>today.Date) return false;
   // Undated and overdue tasks remain actionable today.
   return start || !end || b.Date<=today.Date;
  }
  public static bool Overdue(TaskItem t) { DateTime d; return !t.Done && DateTime.TryParse(t.End,out d) && d.Date<DateTime.Today; }
 }
 public class App : Application {
  [STAThread] public static void Main(string[] args) {
   if(args.Contains("--self-test")){ SelfTest(); return; }
   bool created; using(var mutex=new System.Threading.Mutex(true,"Local\\SideNote.Desktop.v1"+(args.Contains("--preview")?".preview":""),out created)) {
    if(!created) { MessageBox.Show("便签已在运行，请触碰屏幕右侧中段，或点击托盘图标。","SideNote"); return; }
    var app=new App(); app.ShutdownMode=ShutdownMode.OnExplicitShutdown;
    try { if(args.Contains("--preview")) Storage.Dir=Path.Combine(Environment.CurrentDirectory,"qa-data"); var w=new MainWindow(args.Contains("--preview")); app.MainWindow=w; if(!args.Contains("--background")) w.ShowPanel(); if(args.Contains("--preview")&&args.Contains("--render-note"))w.OpenNote(new Note{Title="笔记草稿"}); if(args.Contains("--render")){w.Dispatcher.BeginInvoke(new Action(()=>{w.UpdateLayout();var bmp=new System.Windows.Media.Imaging.RenderTargetBitmap((int)w.ActualWidth,(int)w.ActualHeight,96,96,PixelFormats.Pbgra32);bmp.Render(w);var png=new System.Windows.Media.Imaging.PngBitmapEncoder();png.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bmp));using(var f=File.Create("preview.png"))png.Save(f);app.Shutdown();}),DispatcherPriority.ApplicationIdle);} app.Run(); }
    catch(Exception e){File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"startup-error.txt"),e.ToString());MessageBox.Show("无法打开便签，原始数据未被覆盖。\n"+e.Message,"SideNote");}
   }
  }
  static void SelfTest(){
   var date=new DateTime(2026,11,16); var t=new TaskItem{Start="2026-11-16",End="2026-11-18"};
   if(!Storage.Today(t,date)||!Storage.Today(t,date.AddDays(1))||!Storage.Today(t,date.AddDays(2))||Storage.Today(t,date.AddDays(-1))) throw new Exception("Date interval failed");
   t.Done=true; if(Storage.Today(t,date))throw new Exception("Completed filter failed");
   t=new TaskItem{End="2026-11-18"}; if(Storage.Today(t,date))throw new Exception("Future due date failed");
   Storage.Dir=Path.Combine(Path.GetTempPath(),"SideNote-test-"+Guid.NewGuid());
   try {var s=new State{PanelWidth=328,PanelHeight=510}; s.Sections.Add("收件箱");t.Section="收件箱";s.Tasks.Add(t); s.Notes.Add(new Note{Title="中文笔记",Rtf="test"}); Storage.Save(s); Storage.Save(s); var r=Storage.Load(); if(r.PanelWidth!=328||r.PanelHeight!=510)throw new Exception("Window dimensions round-trip failed");if(r.Tasks[0].Section!="未分类"||r.Sections.Count(x=>x=="未分类")!=1||r.Sections.Contains("收件箱"))throw new Exception("Section migration failed");if(r.Notes[0].Title!="中文笔记"||r.Tasks.Count!=1)throw new Exception("Persistence failed"); File.WriteAllText(Storage.FilePath,"broken"); if(Storage.Load().Notes.Count!=1)throw new Exception("Recovery failed");}
   finally {Directory.Delete(Storage.Dir,true);}
   var doc=new FlowDocument(new Paragraph(new Bold(new Run("中文富文本"))));
   var range=new TextRange(doc.ContentStart,doc.ContentEnd);
   using(var stream=new MemoryStream()){range.Save(stream,DataFormats.Rtf);stream.Position=0;var restored=new FlowDocument();new TextRange(restored.ContentStart,restored.ContentEnd).Load(stream,DataFormats.Rtf);if(!new TextRange(restored.ContentStart,restored.ContentEnd).Text.Contains("中文富文本"))throw new Exception("RTF round-trip failed");}
   File.WriteAllText("test-result.txt","PASS: window dimensions persistence, default section migration, inclusive dates, future dates, completed filter, XML round-trip, backup recovery, Chinese RTF round-trip");
  }
 }
 public class MainWindow : Window {
  State state; StackPanel content; ComboBox category; TextBlock summary; ProgressBar progress;
  bool notes, pinned, dialog, savingError, resizing; string filter="今日";
  Forms.NotifyIcon tray; DispatcherTimer edgeTimer, saveTimer;
  DateTime lastReminderCheck=DateTime.MinValue;
  DateTime edgeSince=DateTime.MinValue, outsideSince=DateTime.MinValue;
  Note current; RichTextBox editor; TextBox noteTitle; ComboBox noteFolder;
  Brush ink=new SolidColorBrush(Color.FromRgb(1,25,1));
  Brush haze=new SolidColorBrush(Color.FromRgb(230,255,248));
  Brush pink=new SolidColorBrush(Color.FromRgb(243,144,163));
  Brush clover=new SolidColorBrush(Color.FromRgb(145,197,26));
  List<Button> filterButtons=new List<Button>();
  Brush muted=new SolidColorBrush(Color.FromRgb(59,114,0));
  public MainWindow(bool preview){
   pinned=preview;
   state=Storage.Load(); Title="SideNote · 便签待办"; Width=state.PanelWidth; MinWidth=320; MinHeight=380;
   WindowStyle=WindowStyle.None; ResizeMode=ResizeMode.CanResize; ShowInTaskbar=false; Topmost=true;
   System.Windows.Shell.WindowChrome.SetWindowChrome(this,new System.Windows.Shell.WindowChrome{CaptionHeight=0,ResizeBorderThickness=new Thickness(7),GlassFrameThickness=new Thickness(0),CornerRadius=new CornerRadius(0),UseAeroCaptionButtons=false});
   SourceInitialized+=(s,e)=>System.Windows.Interop.HwndSource.FromHwnd(new System.Windows.Interop.WindowInteropHelper(this).Handle).AddHook(WindowMessage);
   SizeChanged+=(s,e)=>{if(resizing){state.PanelWidth=ActualWidth;state.PanelHeight=ActualHeight;}};
   Background=new SolidColorBrush(Color.FromRgb(247,255,252)); Foreground=ink;
   FontFamily=new FontFamily(state.Font); FontSize=13; Opacity=state.Opacity;
   Resources.Add(typeof(Button),ButtonStyle());
   foreach(var type in new[]{typeof(TextBox),typeof(ComboBox),typeof(DatePicker),typeof(RichTextBox)}){
    var style=new Style(type);style.Setters.Add(new Setter(Control.ForegroundProperty,ink));style.Setters.Add(new Setter(Control.BackgroundProperty,haze));style.Setters.Add(new Setter(Control.BorderBrushProperty,muted));style.Setters.Add(new Setter(Control.BorderThicknessProperty,new Thickness(1)));Resources.Add(type,style);
   }
   tray=new Forms.NotifyIcon{Icon=System.Drawing.Icon.ExtractAssociatedIcon(System.Reflection.Assembly.GetExecutingAssembly().Location),Text="SideNote · 点击展开",Visible=true};
   Icon=System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(tray.Icon.Handle,Int32Rect.Empty,System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());
   tray.MouseClick+=(s,e)=>{if(e.Button==Forms.MouseButtons.Left)Dispatcher.Invoke(new Action(ShowPanel));};
   var menu=new Forms.ContextMenuStrip(); menu.Items.Add("打开便签",null,(s,e)=>Dispatcher.Invoke(new Action(ShowPanel)));
   menu.Items.Add("退出",null,(s,e)=>Dispatcher.Invoke(new Action(Exit))); tray.ContextMenuStrip=menu;
   saveTimer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(450)}; saveTimer.Tick+=(s,e)=>{saveTimer.Stop();Save();};
   edgeTimer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(40)}; edgeTimer.Tick+=Tick; edgeTimer.Start();
   Closing+=(s,e)=>{e.Cancel=true; HidePanel();}; Build();
  }
  Style ButtonStyle(){
   var st=new Style(typeof(Button));
   st.Setters.Add(new Setter(UIElement.EffectProperty,new System.Windows.Media.Effects.DropShadowEffect{Color=Color.FromRgb(1,25,1),Opacity=.18,BlurRadius=6,ShadowDepth=2,Direction=270}));
   st.Setters.Add(new Setter(Control.PaddingProperty,new Thickness(12,7,12,7)));
   st.Setters.Add(new Setter(Control.MarginProperty,new Thickness(0,0,5,5)));
   st.Setters.Add(new Setter(Control.BackgroundProperty,haze));
   st.Setters.Add(new Setter(Control.ForegroundProperty,ink));
   st.Setters.Add(new Setter(Control.CursorProperty,Cursors.Hand));
   var border=new FrameworkElementFactory(typeof(Border));border.SetValue(Border.CornerRadiusProperty,new CornerRadius(6));border.SetBinding(Border.BackgroundProperty,new System.Windows.Data.Binding("Background"){RelativeSource=new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent)});
   border.SetBinding(Border.BorderBrushProperty,new System.Windows.Data.Binding("BorderBrush"){RelativeSource=new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent)});
   border.SetBinding(Border.BorderThicknessProperty,new System.Windows.Data.Binding("BorderThickness"){RelativeSource=new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent)});
   var presenter=new FrameworkElementFactory(typeof(ContentPresenter));presenter.SetValue(FrameworkElement.HorizontalAlignmentProperty,HorizontalAlignment.Center);presenter.SetValue(FrameworkElement.VerticalAlignmentProperty,VerticalAlignment.Center);presenter.SetBinding(FrameworkElement.MarginProperty,new System.Windows.Data.Binding("Padding"){RelativeSource=new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent)});border.AppendChild(presenter);
   st.Setters.Add(new Setter(Control.TemplateProperty,new ControlTemplate(typeof(Button)){VisualTree=border}));st.Setters.Add(new Setter(Control.BorderThicknessProperty,new Thickness(1)));
   st.Setters.Add(new Setter(Control.BorderBrushProperty,muted));
   st.Setters.Add(new Setter(Control.FontWeightProperty,FontWeights.Medium));
   var selected=new Trigger{Property=FrameworkElement.TagProperty,Value="selected"};selected.Setters.Add(new Setter(Control.BackgroundProperty,clover));selected.Setters.Add(new Setter(Control.BorderBrushProperty,ink));selected.Setters.Add(new Setter(Control.FontWeightProperty,FontWeights.SemiBold));st.Triggers.Add(selected);
   var primary=new Trigger{Property=FrameworkElement.TagProperty,Value="primary"};primary.Setters.Add(new Setter(Control.BackgroundProperty,pink));primary.Setters.Add(new Setter(Control.BorderBrushProperty,ink));primary.Setters.Add(new Setter(Control.FontWeightProperty,FontWeights.SemiBold));st.Triggers.Add(primary);
   var hover=new Trigger{Property=UIElement.IsMouseOverProperty,Value=true};hover.Setters.Add(new Setter(Control.BackgroundProperty,clover));st.Triggers.Add(hover);
   var pressed=new Trigger{Property=ButtonBase.IsPressedProperty,Value=true};pressed.Setters.Add(new Setter(UIElement.EffectProperty,new System.Windows.Media.Effects.DropShadowEffect{Color=Color.FromRgb(1,25,1),Opacity=.10,BlurRadius=3,ShadowDepth=.5,Direction=270}));st.Triggers.Add(pressed);
   return st;
  }
  Button Btn(string title,Action action,string role=""){var b=new Button{Content=title,Tag=role};b.Click+=(s,e)=>action();return b;}
  TextBlock Text(string s,double size,Brush color){return new TextBlock{Text=s,FontSize=size,Foreground=color,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,8)};}
  IntPtr WindowMessage(IntPtr hwnd,int message,IntPtr wParam,IntPtr lParam,ref bool handled){
   if(message==0x0231){resizing=true;outsideSince=DateTime.MinValue;}
   if(message==0x0232){resizing=false;PositionPanel();Save();outsideSince=DateTime.MinValue;}
   return IntPtr.Zero;
  }
  void PositionPanel(){
   var area=Forms.Screen.PrimaryScreen.WorkingArea;var source=PresentationSource.FromVisual(this);double scale;
   if(source==null){using(var graphics=System.Drawing.Graphics.FromHwnd(IntPtr.Zero))scale=graphics.DpiX/96.0;}else scale=source.CompositionTarget.TransformToDevice.M11;
   MaxWidth=Math.Max(MinWidth,area.Width/scale-16);MaxHeight=Math.Max(MinHeight,area.Height/scale-24);
   Width=Math.Max(MinWidth,Math.Min(state.PanelWidth,MaxWidth));
   Height=Math.Max(MinHeight,Math.Min(state.PanelHeight>0?state.PanelHeight:area.Height/scale*.5,MaxHeight));
   Left=area.Right/scale-Width-8;Top=area.Top/scale+(area.Height/scale-Height)/2;
  }
  public void ShowPanel(){PositionPanel();Show();outsideSince=DateTime.MinValue;}
  void HidePanel(){FlushNote();Save();Hide();edgeSince=DateTime.MinValue;}
  void Tick(object sender,EventArgs e){
   var p=Forms.Cursor.Position;var area=Forms.Screen.PrimaryScreen.WorkingArea;
   if(!IsVisible){bool hit=p.X>=area.Right-2 && p.X<area.Right && p.Y>=area.Top+area.Height*.25 && p.Y<=area.Top+area.Height*.75;
    if(hit){if(edgeSince==DateTime.MinValue)edgeSince=DateTime.Now;if((DateTime.Now-edgeSince).TotalMilliseconds>=100)ShowPanel();}else edgeSince=DateTime.MinValue;
   }else if(!pinned&&!dialog&&!resizing){var src=PresentationSource.FromVisual(this);double scale=src==null?1:src.CompositionTarget.TransformToDevice.M11;
    bool inside=p.X>=Left*scale-8&&p.X<=(Left+ActualWidth)*scale+8&&p.Y>=Top*scale-8&&p.Y<=(Top+ActualHeight)*scale+8;
    if(inside || (current!=null && IsKeyboardFocusWithin)){outsideSince=DateTime.MinValue;}else{if(outsideSince==DateTime.MinValue)outsideSince=DateTime.Now;if((DateTime.Now-outsideSince).TotalMilliseconds>600)HidePanel();}
   }
   if((DateTime.Now-lastReminderCheck).TotalSeconds<10)return;lastReminderCheck=DateTime.Now;
   foreach(var t in state.Tasks){DateTime when;if(!t.Done&&DateTime.TryParse(t.Reminder,out when)&&when<=DateTime.Now&&t.Notified!=t.Reminder){t.Notified=t.Reminder;tray.ShowBalloonTip(10000,"待办提醒",t.Title,Forms.ToolTipIcon.Info);Save();}}
  }
  void Save(){try{Storage.Save(state);savingError=false;}catch(Exception e){if(!savingError){savingError=true;MessageBox.Show("保存失败，请保留窗口并检查磁盘权限。\n"+e.Message);}}}
  void Exit(){FlushNote();Save();var icon=tray.Icon;tray.Dispose();icon.Dispose();edgeTimer.Stop();Application.Current.Shutdown();}
  string Selected {get{return category==null||category.SelectedItem==null?"全部":category.SelectedItem.ToString();}}
  void Build(){
   FlushNote();current=null;editor=null;noteTitle=null;noteFolder=null;
   var root=new DockPanel{Margin=new Thickness(22,18,18,15)};Content=root;
   var head=new StackPanel();DockPanel.SetDock(head,Dock.Top);root.Children.Add(head);
   var top=new DockPanel();var controls=new StackPanel{Orientation=Orientation.Horizontal};DockPanel.SetDock(controls,Dock.Right);top.Children.Add(controls);
   controls.Children.Add(Btn(pinned?"已固定":"固定",()=>{pinned=!pinned;Build();}));controls.Children.Add(Btn("⚙",Settings));controls.Children.Add(Btn("收起",HidePanel));
   foreach(Button button in controls.Children)button.Padding=new Thickness(8,6,8,6);
   top.Children.Add(Text("SideNote",21,ink));head.Children.Add(top);
   head.Children.Add(Text(DateTime.Today.ToString("M 月 d 日 · dddd"),11,muted));
   var tabs=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(0,8,0,10)};
   tabs.Children.Add(Btn(notes?"待办":"● 待办",()=>{notes=false;Build();},notes?"":"selected"));tabs.Children.Add(Btn(notes?"● 笔记":"笔记",()=>{notes=true;Build();},notes?"selected":""));head.Children.Add(tabs);
   summary=Text("",12,muted);head.Children.Add(summary);progress=new ProgressBar{Height=6,Maximum=100,Foreground=muted,Background=clover,BorderBrush=muted,BorderThickness=new Thickness(1),Margin=new Thickness(0,0,0,14)};head.Children.Add(progress);
   var tools=new DockPanel();var add=Btn(notes?"＋ 笔记":"＋ 待办",()=>{if(notes)NewNote();else EditTask(null);},"primary");DockPanel.SetDock(add,Dock.Right);tools.Children.Add(add);
   category=new ComboBox{Margin=new Thickness(0,0,8,5),MinWidth=95,VerticalContentAlignment=VerticalAlignment.Center};category.Items.Add("全部");foreach(var c in notes?state.Folders.OrderBy(x=>x).ToList():state.Sections)category.Items.Add(c);category.SelectedIndex=0;category.SelectionChanged+=(s,e)=>Render();tools.Children.Add(category);head.Children.Add(tools);
   filterButtons.Clear();var options=new WrapPanel();options.Children.Add(Btn(notes?"＋ 文件夹":"＋ 分区",AddCategory));
   if(!notes)foreach(var f in new[]{"今日","全部","已完成"}){string capture=f;var choice=Btn(f,()=>{filter=capture;Render();},filter==f?"selected":"");filterButtons.Add(choice);options.Children.Add(choice);}head.Children.Add(options);
   var scroll=new ScrollViewer{VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};content=new StackPanel();scroll.Content=content;root.Children.Add(scroll);Render();
  }
  void Render(){if(content==null)return;content.Children.Clear();foreach(var choice in filterButtons)choice.Tag=choice.Content.ToString()==filter?"selected":"";
   int active=state.Tasks.Count(t=>Storage.Today(t,DateTime.Today));int done=state.Tasks.Count(t=>t.Done && t.Completed==DateTime.Today.ToString("yyyy-MM-dd"));summary.Text=notes?state.Notes.Count+" 篇笔记 · 自动保存": "今天还有 "+active+" 条待办  ·  已完成 "+done+" 条";progress.Value=done+active==0?0:100.0*done/(done+active);progress.Visibility=notes?Visibility.Collapsed:Visibility.Visible;
   if(notes){foreach(var n in state.Notes.Where(n=>Selected=="全部"||(n.Folder==Selected||n.Folder.StartsWith(Selected+"/"))).OrderByDescending(n=>n.Updated)){var row=new StackPanel();row.Children.Add(Text(n.Title,15,ink));row.Children.Add(Text(n.Folder+"  ·  "+n.Updated.ToString("MM-dd HH:mm"),11,muted));var b=new Button{Content=row,HorizontalContentAlignment=HorizontalAlignment.Stretch,Margin=new Thickness(0,5,0,6)};var capture=n;b.Click+=(s,e)=>OpenNote(capture);content.Children.Add(b);}}
   else {foreach(var t in state.Tasks.Where(t=>(Selected=="全部"||t.Section==Selected)&&(filter=="已完成"?t.Done:!t.Done&&(filter!="今日"||Storage.Today(t,DateTime.Today)))).OrderByDescending(t=>t.Priority).ThenBy(t=>t.End)){
    var row=new DockPanel{Margin=new Thickness(0,10,0,8)};var check=new CheckBox{IsChecked=t.Done,VerticalAlignment=VerticalAlignment.Top,Margin=new Thickness(0,5,10,0)};DockPanel.SetDock(check,Dock.Left);row.Children.Add(check);var capture=t;check.Click+=(s,e)=>{capture.Done=check.IsChecked==true;capture.Completed=capture.Done?DateTime.Today.ToString("yyyy-MM-dd"):"";Save();Render();};
    var info=new StackPanel();var title=Text(t.Title,14,ink);if(t.Done)title.TextDecorations=TextDecorations.Strikethrough; info.Children.Add(title);
    string dates=t.Start!=""?t.Start+" → "+(t.End==""?"无截止":t.End):(t.End==""?"无日期":t.End+" 截止");
    info.Children.Add(Text(t.Section+" · "+new[]{"普通","重要","紧急"}[t.Priority]+" · "+dates+(Storage.Overdue(t)?" · 已逾期":"")+(t.Reminder!=""?" · 提醒":""),10,muted));
    var button=new Button{Content=info,HorizontalContentAlignment=HorizontalAlignment.Stretch,BorderThickness=new Thickness(3,0,0,0),BorderBrush=(Brush)new BrushConverter().ConvertFromString(t.Color),Margin=new Thickness(0),Padding=new Thickness(9,3,2,3)};button.Click+=(s,e)=>EditTask(capture);row.Children.Add(button);content.Children.Add(row);
   }}
   if(content.Children.Count==0)content.Children.Add(Text(notes?"把想法留在这里。\n新建一篇笔记，随时回来继续写。":"这里暂时没有待办。\n把要做的第一件事记下来吧。",14,muted));
  }
  Window Dialog(string title,int height){dialog=true;var w=new Window{Title=title,Width=390,Height=height,Owner=this,WindowStartupLocation=WindowStartupLocation.CenterOwner,Background=Background,FontFamily=FontFamily,FontSize=13,ResizeMode=ResizeMode.NoResize,ShowInTaskbar=false,Topmost=true,Foreground=ink};w.Resources=Resources;w.Closed+=(s,e)=>{dialog=false;outsideSince=DateTime.MinValue;};return w;}
  TextBox Field(Panel p,string label,string value){p.Children.Add(Text(label,12,muted));var b=new TextBox{Text=value,Padding=new Thickness(7),Margin=new Thickness(0,0,0,10)};p.Children.Add(b);return b;}
  ComboBox Select(Panel p,string label,IEnumerable<string> items,string selected){p.Children.Add(Text(label,12,muted));var c=new ComboBox{Margin=new Thickness(0,0,0,10),Padding=new Thickness(5)};foreach(var i in items)c.Items.Add(i);c.SelectedItem=selected;if(c.SelectedIndex<0)c.SelectedIndex=0;p.Children.Add(c);return c;}
  void AddCategory(){var w=Dialog(notes?"新建文件夹":"新建分区",205);var p=new StackPanel{Margin=new Thickness(20)};w.Content=p;var name=Field(p,notes?"名称（用 / 表示子文件夹，如 工作/草稿）":"分区名称","");p.Children.Add(Btn("创建",()=>{string value=name.Text.Trim();var list=notes?state.Folders:state.Sections;if(value==""||value=="全部"||list.Contains(value)){MessageBox.Show(w,"请输入不重复的名称。");return;}if(notes){string path="";foreach(var part in value.Split('/')){if(string.IsNullOrWhiteSpace(part)){MessageBox.Show(w,"文件夹层级名称不能为空。");return;}path=path==""?part.Trim():path+"/"+part.Trim();if(!list.Contains(path))list.Add(path);}value=path;}else list.Add(value);Save();w.Close();Build();category.SelectedItem=value;}));w.ShowDialog();}
  void EditTask(TaskItem original){
   var t=original??new TaskItem();var w=Dialog(original==null?"新建待办":"编辑待办",650);var scroll=new ScrollViewer{VerticalScrollBarVisibility=ScrollBarVisibility.Auto};var p=new StackPanel{Margin=new Thickness(22)};scroll.Content=p;w.Content=scroll;
   var title=Field(p,"要做什么",t.Title);var section=Select(p,"所属分区",state.Sections,original==null&&Selected!="全部"?Selected:t.Section);
   p.Children.Add(Text("起始日期（可留空）",12,muted));var start=new DatePicker{Margin=new Thickness(0,0,0,10)};DateTime dt;if(DateTime.TryParse(t.Start,out dt))start.SelectedDate=dt;p.Children.Add(start);
   p.Children.Add(Text("截止日期（可留空）",12,muted));var end=new DatePicker{Margin=new Thickness(0,0,0,10)};if(DateTime.TryParse(t.End,out dt))end.SelectedDate=dt;p.Children.Add(end);
   p.Children.Add(Text("设置起止日期后，期间每天都显示在今日待办。",11,muted));
   var priority=Select(p,"紧急程度",new[]{"普通","重要","紧急"},new[]{"普通","重要","紧急"}[t.Priority]);
   var colors=new List<string>{"薄荷 · Haze","粉色 · Gumball","亮绿 · Clover","深绿 · Lilypad","墨绿 · After Eight"};var hex=new List<string>{"#E6FFF8","#F390A3","#91C51A","#3B7200","#011901"};if(!hex.Contains(t.Color)){colors.Add("原有颜色");hex.Add(t.Color);}var color=Select(p,"标记颜色",colors,colors[hex.IndexOf(t.Color)]);
   var remind=new CheckBox{Content="开启提醒（应用需在运行）",IsChecked=t.Reminder!="",Margin=new Thickness(0,0,0,8)};p.Children.Add(remind);var time=Field(p,"提醒时间 · yyyy-MM-dd HH:mm",t.Reminder==""?DateTime.Now.AddHours(1).ToString("yyyy-MM-dd HH:mm"):t.Reminder);time.IsEnabled=remind.IsChecked==true;remind.Click+=(s,e)=>time.IsEnabled=remind.IsChecked==true;
   var actions=new WrapPanel();p.Children.Add(actions);actions.Children.Add(Btn("保存",()=>{
    DateTime r;if(title.Text.Trim()==""){MessageBox.Show(w,"请填写待办内容。");return;}if(start.SelectedDate.HasValue&&end.SelectedDate.HasValue&&end.SelectedDate<start.SelectedDate){MessageBox.Show(w,"截止日期不能早于起始日期。");return;}
    if(remind.IsChecked==true&&!DateTime.TryParse(time.Text,out r)){MessageBox.Show(w,"请填写有效的提醒时间，例如 2026-11-16 18:00。");return;}
    t.Title=title.Text.Trim();t.Section=section.SelectedItem.ToString();t.Start=start.SelectedDate.HasValue?start.SelectedDate.Value.ToString("yyyy-MM-dd"):"";t.End=end.SelectedDate.HasValue?end.SelectedDate.Value.ToString("yyyy-MM-dd"):"";t.Priority=priority.SelectedIndex;t.Color=hex[color.SelectedIndex];t.Reminder=remind.IsChecked==true?DateTime.Parse(time.Text).ToString("yyyy-MM-dd HH:mm"):"";
    if(original==null)state.Tasks.Add(t);Save();w.Close();Render();}));
   if(original!=null)actions.Children.Add(Btn("删除",()=>{if(MessageBox.Show(w,"删除这条待办？","确认删除",MessageBoxButton.YesNo)==MessageBoxResult.Yes){state.Tasks.Remove(t);Save();w.Close();Render();}}));title.Focus();w.ShowDialog();
  }
  void NewNote(){var n=new Note{Folder=Selected=="全部"?state.Folders[0]:Selected};state.Notes.Add(n);Save();OpenNote(n);}
  internal void OpenNote(Note n){
   current=n;var root=new DockPanel{Margin=new Thickness(18)};Content=root;var head=new StackPanel();DockPanel.SetDock(head,Dock.Top);root.Children.Add(head);
   var bar=new WrapPanel();bar.Children.Add(Btn("‹ 笔记列表",Build));bar.Children.Add(Btn("收起",HidePanel));bar.Children.Add(Btn("删除",()=>{if(MessageBox.Show(this,"删除这篇笔记？","确认删除",MessageBoxButton.YesNo)==MessageBoxResult.Yes){state.Notes.Remove(n);current=null;Save();Build();}}));head.Children.Add(bar);
   noteTitle=Field(head,"标题 · 自动保存",n.Title);noteFolder=Select(head,"文件夹",state.Folders,n.Folder);
   var toolbar=new WrapPanel{Margin=new Thickness(0,2,0,6)};head.Children.Add(toolbar);
   var body=new Border{BorderBrush=muted,BorderThickness=new Thickness(1.5),CornerRadius=new CornerRadius(6),Background=Brushes.White,Padding=new Thickness(3)};
   editor=new RichTextBox{AcceptsTab=true,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,BorderThickness=new Thickness(0),Background=Brushes.Transparent,Padding=new Thickness(8),FontSize=14};body.Child=editor;root.Children.Add(body);
   if(n.Rtf!=""){using(var ms=new MemoryStream(Convert.FromBase64String(n.Rtf)))new TextRange(editor.Document.ContentStart,editor.Document.ContentEnd).Load(ms,DataFormats.Rtf);}
   var format=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(0,0,6,0)};toolbar.Children.Add(format);
   AddCommand(format,"B","加粗 · Ctrl+B",EditingCommands.ToggleBold).FontWeight=FontWeights.Bold;
   AddCommand(format,"I","斜体 · Ctrl+I",EditingCommands.ToggleItalic).FontStyle=FontStyles.Italic;
   AddCommand(format,"U","下划线 · Ctrl+U",EditingCommands.ToggleUnderline).Content=new TextBlock{Text="U",TextDecorations=TextDecorations.Underline};
   var strike=ToolButton("S","删除线");strike.Content=new TextBlock{Text="S",TextDecorations=TextDecorations.Strikethrough};strike.Click+=(s,e)=>{
    var value=editor.Selection.GetPropertyValue(Inline.TextDecorationsProperty) as TextDecorationCollection;
    var decorations=value==null?new TextDecorationCollection():value.Clone();
    var existing=decorations.Where(d=>d.Location==TextDecorationLocation.Strikethrough).ToList();
    if(existing.Count>0){foreach(var decoration in existing)decorations.Remove(decoration);}else decorations.Add(TextDecorations.Strikethrough[0]);
    editor.Selection.ApplyPropertyValue(Inline.TextDecorationsProperty,decorations);editor.Focus();
   };format.Children.Add(strike);
   var lists=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(0,0,6,0)};toolbar.Children.Add(lists);
   AddCommand(lists,"•≡","项目符号",EditingCommands.ToggleBullets).FontSize=13;
   AddCommand(lists,"1.","有序列表",EditingCommands.ToggleNumbering);
   AddCommand(lists,"⇥","增加缩进",EditingCommands.IncreaseIndentation);
   var history=new StackPanel{Orientation=Orientation.Horizontal};toolbar.Children.Add(history);
   AddCommand(history,"↶","撤销 · Ctrl+Z",ApplicationCommands.Undo);
   AddCommand(history,"↷","重做 · Ctrl+Y",ApplicationCommands.Redo);
   noteTitle.TextChanged+=(s,e)=>QueueSave();noteFolder.SelectionChanged+=(s,e)=>QueueSave();editor.TextChanged+=(s,e)=>QueueSave();editor.Focus();
  }
  Button ToolButton(string symbol,string description){var b=new Button{Content=symbol,ToolTip=description,Width=24,Height=24,Padding=new Thickness(0),Margin=new Thickness(0,0,4,6),FontSize=14,HorizontalContentAlignment=HorizontalAlignment.Center,VerticalContentAlignment=VerticalAlignment.Center,Focusable=false};System.Windows.Automation.AutomationProperties.SetName(b,description);return b;}
  Button AddCommand(Panel p,string symbol,string description,ICommand command){var b=ToolButton(symbol,description);b.Command=command;b.CommandTarget=editor;p.Children.Add(b);return b;}
  void QueueSave(){FlushNote();saveTimer.Stop();saveTimer.Start();}
  void FlushNote(){if(current==null||editor==null)return;current.Title=string.IsNullOrWhiteSpace(noteTitle.Text)?"未命名笔记":noteTitle.Text;current.Folder=noteFolder.SelectedItem.ToString();using(var ms=new MemoryStream()){new TextRange(editor.Document.ContentStart,editor.Document.ContentEnd).Save(ms,DataFormats.Rtf);current.Rtf=Convert.ToBase64String(ms.ToArray());}current.Updated=DateTime.Now;}
  void Settings(){FlushNote();var w=Dialog("设置",360);var p=new StackPanel{Margin=new Thickness(22)};w.Content=p;
   p.Children.Add(Text("窗口透明度",13,ink));var slider=new Slider{Minimum=.55,Maximum=1,Value=state.Opacity,Margin=new Thickness(0,8,0,16)};p.Children.Add(slider);slider.ValueChanged+=(s,e)=>{state.Opacity=slider.Value;Opacity=slider.Value;};
   var fonts=Fonts.SystemFontFamilies.Select(f=>f.Source).OrderBy(f=>f).ToArray();var font=Select(p,"字体（仅显示本机已安装字体）",fonts,state.Font);font.SelectionChanged+=(s,e)=>{state.Font=font.SelectedItem.ToString();FontFamily=new FontFamily(state.Font);};
   var auto=new CheckBox{Content="开机自动启动（收起到侧边）",Margin=new Thickness(0,8,0,16)};using(var key=Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run")){auto.IsChecked=key!=null&&key.GetValue("SideNote")!=null;}p.Children.Add(auto);
   auto.Click+=(s,e)=>{try{using(var key=Registry.CurrentUser.CreateSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run")){if(auto.IsChecked==true)key.SetValue("SideNote","\""+System.Reflection.Assembly.GetExecutingAssembly().Location+"\" --background");else key.DeleteValue("SideNote",false);}}catch(Exception ex){MessageBox.Show(w,ex.Message);}};
   p.Children.Add(Text("右侧中段触边展开 · 托盘图标可随时打开\n提醒在程序运行时生效；错过的提醒会在下次启动补发。",11,muted));p.Children.Add(Btn("完成",()=>{Save();w.Close();}));w.ShowDialog();
  }
 }
}


