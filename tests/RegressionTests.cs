using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using T50LabelPrinter;

static class RegressionTests
{
    static int assertions;
    static void Assert(bool value, string message) { assertions++; if (!value) throw new Exception(message); }
    static object Field(object obj, string name) { return obj.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(obj); }
    static object Call(object obj, string name, params object[] args) { return obj.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(obj,args); }
    static void Reject(Action action, string message) { bool rejected=false; try { action(); } catch(InvalidOperationException) { rejected=true; } Assert(rejected,message); }
    [STAThread]
    static int Main(string[] args)
    {
        try
        {
            string output = Path.GetFullPath(args.Length > 0 ? args[0] : "dist/verification"); Directory.CreateDirectory(output);
            ThermalScheduleDocument doc = ThermalScheduleDocument.CreateDefault();
            doc.Title = "日期、合并空行、自由图片";
            doc.Items = new List<ThermalScheduleItem> {
                new ThermalScheduleItem { Kind=ThermalScheduleItemKind.Date, Content="今天：{日期}" },
                new ThermalScheduleItem { Content="行内日期 {日期}，任务 A" },
                new ThermalScheduleItem(), new ThermalScheduleItem(), new ThermalScheduleItem(),
                new ThermalScheduleItem { Kind=ThermalScheduleItemKind.Countdown, Content="目标日", TargetDate=DateTime.Today.AddDays(3) }
            };
            doc.Normalize();
            Assert(doc.Items[0].GetDisplayContent(new DateTime(2026,10,9))=="今天：2026-10-09","date object");
            Assert(doc.Items[1].GetDisplayContent(new DateTime(2026,10,9)).Contains("2026-10-09"),"inline date");
            Assert(ThermalScheduleRenderer.GetRowBounds(doc).Count==6,"empty rows visible");
            Assert(!ThermalScheduleRowOperations.CanMerge(doc.Items,new[]{0,1}),"nonempty/special rows reject merge");
            Assert(!ThermalScheduleRowOperations.CanMerge(doc.Items,new[]{2,4}),"nonadjacent merge");
            var before=ThermalScheduleRenderer.GetRowBounds(doc);
            float total=before[2].Height+before[3].Height+before[4].Height;
            ThermalScheduleRowOperations.Merge(doc.Items,new[]{2,3,4});
            Assert(doc.Items.Count==4 && doc.Items[2].RowSpan==3,"merge three rows");
            Assert(Math.Abs(ThermalScheduleRenderer.GetRowBounds(doc)[2].Height-total)<0.02,"merge preserves row height");
            doc.Items[2].Content="禁止拆分";
            Reject(()=>ThermalScheduleRowOperations.Split(doc.Items,new[]{2}),"nonempty split blocked");
            doc.Items[2].Content=""; doc.Items[2].Time="12:00";
            Reject(()=>ThermalScheduleRowOperations.Split(doc.Items,new[]{2}),"hidden time split blocked");
            doc.Items[2].Time=""; doc.Items[2].Completed=true;
            Reject(()=>ThermalScheduleRowOperations.Split(doc.Items,new[]{2}),"completed split blocked");
            doc.Items[2].Completed=false;
            ThermalScheduleRowOperations.Split(doc.Items,new[]{2});
            Assert(doc.Items.Count==6 && doc.Items.All(item=>item.RowSpan==1),"split restores three rows");
            var start=new ThermalScheduleImage{X=5,Y=10,Width=20,Height=10};
            for(int handle=0;handle<8;handle++) {
                var resized=start.DeepClone(); resized.ResizeFrom(start,handle,5,4,true);
                Assert(Math.Abs(resized.Width/resized.Height-2m)<0.0001m,"aspect handle "+handle);
                Assert(resized.X>=0 && resized.X+resized.Width<=58 && resized.Y>=0 && resized.Y+resized.Height<=1000,"resize bounds "+handle);
            }
            var free=start.DeepClone(); free.ResizeFrom(start,4,5,8,false);
            Assert(free.Width==25 && free.Height==18,"free resize");
            free.ResizeFrom(start,0,100,100,true);
            Assert(free.Width>=1 && free.Height>=1 && free.X+free.Width<=58,"minimum proportional resize");
            string sample=Path.Combine(output,"sample.png");
            using(var bitmap=new Bitmap(120,60)) { using(var g=Graphics.FromImage(bitmap)) { g.Clear(Color.White); g.FillEllipse(Brushes.Black,2,2,56,56); g.DrawRectangle(Pens.Black,65,8,48,44); } bitmap.Save(sample); }
            var data=ImageAssetService.Import(sample);
            var image=new ThermalScheduleImage { FileName=data.FileName,ImageData=data.PngBase64,X=3,Y=60,Width=30,Height=15,KeepAspect=true,Threshold=128,Dither=true };
            doc.Images.Add(image);
            var clone=doc.DeepClone(); clone.Images[0].X=9;
            Assert(doc.Images[0].X==3,"image deep clone");
            using(var bitmap=ThermalScheduleRenderer.Render(doc)) {
                Assert(bitmap.Width==464 && bitmap.Height>=600,"image extends receipt");
                Assert(bitmap.GetPixel(24+30,480+30).R==0,"image rendered in output");
                bitmap.Save(Path.Combine(output,"receipt.png"));
            }
            string file=Path.Combine(output,"roundtrip.t58schedule"); var store=new ThermalScheduleTemplateStore();
            ThermalScheduleRowOperations.Merge(doc.Items,new[]{2,3,4}); store.Save(file,doc);
            var loaded=store.Load(file);
            Assert(loaded.Items[2].RowSpan==3 && loaded.Items[0].Kind==ThermalScheduleItemKind.Date,"object template roundtrip");
            Assert(loaded.Images.Count==1 && loaded.Images[0].ImageData==image.ImageData && loaded.Images[0].KeepAspect,"embedded image roundtrip");
            store.Save(file,loaded); Assert(store.Load(file).Items.Count==4,"atomic overwrite");
            string legacy=Path.Combine(output,"legacy.t58schedule");
            File.WriteAllText(legacy,"{\"Format\":\"T50LabelPrinter.ThermalSchedule\",\"Version\":1,\"Document\":{\"Title\":\"旧模板\",\"AutoDate\":true,\"Items\":[{\"Content\":\"旧日程\"}]}}",Encoding.UTF8);
            var old=store.Load(legacy); Assert(old.Items[0].RowSpan==1 && old.Images.Count==0,"version 1 migration");
            using(var page=new ThermalSchedulePage()) {
                Call(page,"LoadDocument",loaded);
                var grid=(DataGridView)Field(page,"_items");
                Assert(grid.MultiSelect,"grid multiselect");
                grid.Font=new Font("Microsoft YaHei UI", 18f);
                Assert(grid.Rows.Cast<DataGridViewRow>().All(row=>row.Height>=grid.Font.Height+12),"large font existing rows have padding");
                int newRow=grid.Rows.Add("日程",false,"","","");
                Assert(grid.Rows[newRow].Height>=grid.Font.Height+12,"large font newly added rows have padding");
                grid.Rows.RemoveAt(newRow);
                grid.ClearSelection(); grid.Rows[1].Selected=true; grid.Rows[2].Selected=true;
                Call(page,"ItemsCellMouseDown",grid,new DataGridViewCellMouseEventArgs(3,2,0,0,new MouseEventArgs(MouseButtons.Right,1,0,0,0)));
                Assert(grid.SelectedRows.Count==2,"rightclick retains multiselection");
                Call(page,"AddEmptyItem");
                Assert(grid.Rows.Count==5,"insert empty object");
                var rebuilt=(ThermalScheduleDocument)Call(page,"BuildDocument",true);
                Assert(rebuilt.Images.Count==1,"page retains embedded images");
                page.Size=new Size(1100,780); page.CreateControl();
                using(var rendered=new Bitmap(page.Width,page.Height)) { page.DrawToBitmap(rendered,new Rectangle(Point.Empty,page.Size)); rendered.Save(Path.Combine(output,"schedule-page.png")); }
            }
            using(var page=new EasyUpdatePage()) { page.Size=new Size(1050,650); page.CreateControl(); using(var bitmap=new Bitmap(page.Width,page.Height)) { page.DrawToBitmap(bitmap,new Rectangle(Point.Empty,page.Size)); bitmap.Save(Path.Combine(output,"update-page.png")); } }
            Console.WriteLine("PASS "+assertions+" assertions: dates, merge/split, resize, rendering, templates, page interactions.");
            return 0;
        } catch(Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
}

