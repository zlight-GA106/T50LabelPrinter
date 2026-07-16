using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Forms;

namespace Supvan.T50PRO.SDK.Demo
{
    /// <summary>
    /// 标牌机SDKDemo
    /// </summary>
    public partial class frmDemo : Form
    {
        /// <summary>
        /// 构造函数
        /// </summary>
        public frmDemo()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 查询连接的设备
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnSearch_Click(object sender, EventArgs e)
        {
            cmbDevicePaths.Items.Clear();
            cmbDevicePaths.Items.AddRange(T50PROPrintUtil.GetDevicePaths().ToArray());
            cmbDevicePaths.SelectedIndex = cmbDevicePaths.Items.Count - 1;
        }

        /// <summary>
        /// 打印
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnPrint_Click(object sender, EventArgs e)
        {
            string devicePath = cmbDevicePaths.SelectedItem != null ? cmbDevicePaths.SelectedItem.ToString() : "";
            //参数模型
            SDKSPParamter sDKTPParamter = new SDKSPParamter();
            //打印设置
            sDKTPParamter.PrintSet = new SDKPrintSet() { Copy = 2,  Deepness = 4, DPI = 8f,  Direction=0, Gap=3, Speed=40, Width=50, Height=30, PaperType=1, MaxDotValue = 384, OffsetH = 0, OffsetV = 0, OneByOne = true };
            //打印文件
            sDKTPParamter.PrintPages = new List<SDKPrintPage>()
            {
                //打印页面
                new SDKPrintPage()
                {
                    Repeat=1,
                    //页内对象
                    DrawObjects=new List<SDKPrintPageDrawObject>()
                    {
                        new SDKPrintPageDrawObject() 
                        {
                            AntiColor= false,
                            X= 15,
                            Y= 3,
                            Width= 15,
                            Height=4,
                            Content="郑州市公安局",
                            FontName="黑体",
                            FontStyle=0,
                            Align=1,
                            FontSize="4",
                            AutoReturn=false,
                            Format="TEXT"
                        },
                        new SDKPrintPageDrawObject()
                        {
                            AntiColor= false,
                            X= 3,
                            Y= 12,
                            Width= 28,
                            Height=4,
                            Content= "资产名称：便携式计算机",
                            FontName= "黑体",
                            FontStyle= 0,
                            Align=0,
                            FontSize= "3",
                            AutoReturn= false,
                            Format= "TEXT"
                        },
                        new SDKPrintPageDrawObject()
                        {
                            AntiColor=false,
                            X=3,
                            Y= 17,
                            Width=28,
                            Height= 4,
                            Content="建卡时间：2022-02-17",
                            FontName= "黑体",
                            FontStyle=0,
                            Align=0,
                            FontSize= "3",
                            AutoReturn=false,
                            Format= "TEXT"
                        },
                        new SDKPrintPageDrawObject()
                        {
                            AntiColor= false,
                            X= 3,
                            Y= 22,
                            Width= 28,
                            Height= 4,
                            Content= "资产编码：20220001",
                            FontName= "黑体",
                            FontStyle= 0,
                            Align=0,
                            FontSize="3",
                            AutoReturn=false,
                            Format= "TEXT"
                        },
                        new SDKPrintPageDrawObject()
                        {
                            AntiColor=false,
                            X= 36,
                            Y= 17,
                            Width= 10,
                            Height= 10,
                            Content= "1215484152132165315",
                            FontName="黑体",
                            FontStyle= 0,
                            FontSize= "0",
                            AutoReturn= false,
                            Format= "QRCODE"
                        }
                    }
              },
            };

            //查询状态
            PrintResult printResult = T50PROPrintUtil.GetPrintResult(devicePath);
            if(printResult!=null)
            {
                lblmsg.Text = printResult.PrintDes + "\n" + printResult.ErrorMsg;
            }

            //状态判断
            if (printResult != null && printResult.State == DeviceState.Waiting)
            {
                //USB通讯必须子线程
                new Thread(() =>
                {
                    T50PROPrintUtil.DoPrint(sDKTPParamter, devicePath);
                }).Start();
            }
        }
    }
}
