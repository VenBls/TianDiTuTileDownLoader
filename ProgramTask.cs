using Newtonsoft.Json;
using System.Configuration;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.Net;
using System.Text.RegularExpressions;
using static System.Net.Mime.MediaTypeNames;

namespace ThreadLearn
{
    internal class Program
    {
        static string token = ConfigurationManager.AppSettings.Get("token");
        static string imagType = ConfigurationManager.AppSettings.Get("imagType");
        private static int threadCount = 256;
        static string leftUpLonStr = ConfigurationManager.AppSettings.Get("leftUpLonStr");
        static string leftUpLatStr = ConfigurationManager.AppSettings.Get("leftUpLatStr");
        static string rightDownLonStr = ConfigurationManager.AppSettings.Get("rightDownLonStr");
        static string rightDownLatStr = ConfigurationManager.AppSettings.Get("rightDownLatStr");
        static string downLoadPath = ConfigurationManager.AppSettings.Get("downLoadPath");
        static async Task Main(string[] args)
        {
            #region 天地图瓦片下载和本地化
            double leftUpLon, leftUpLat, rightDownLon, rightDownLat;
            int layerMax;
            Console.Clear();
            Console.WriteLine("请输入左上角经度");
            leftUpLon = double.Parse(leftUpLonStr);
            Console.WriteLine(leftUpLon);
            Console.WriteLine("请输入左上角纬度");
            leftUpLat = double.Parse(leftUpLatStr);
            Console.WriteLine(leftUpLat);
            Console.WriteLine("请输入右下角经度");
            rightDownLon = double.Parse(rightDownLonStr);
            Console.WriteLine(rightDownLon);
            Console.WriteLine("请输入右下角纬度");
            rightDownLat = double.Parse(rightDownLatStr);
            Console.WriteLine(rightDownLat);
            Console.WriteLine("请输入要下载的最大层级0-18");
            layerMax = int.Parse(Console.ReadLine());
            Console.WriteLine(layerMax);
            Console.Clear();
            List<TileID> tileIDs = CaclulatTiles(layerMax, leftUpLon, leftUpLat, rightDownLon, rightDownLat);
            List<DownLoadInfo> reloadInfos = new List<DownLoadInfo>();
            int count = 0;
            Console.WriteLine("计算瓦片数量中");
            foreach (var item in tileIDs)
            {
                //Console.WriteLine(item.z + "-" + item.x + "-" + item.y);
                count++;
            }
            Console.Clear();
            Console.WriteLine("总计瓦片数:" + count);
            Console.WriteLine("总计" + count + "个" + ",预计占据空间" + 8 * count / 1024 + "MB" + "是否开启下载Y/N");
            string input = Console.ReadLine();
            if (input == "Y" || input == "y")
            {
                Console.Clear();
                Console.WriteLine("请指定下载路径形如D:\\Test\\");
                string path = downLoadPath;
                Console.WriteLine(path);
                #region 天地图官方的下载
                List<Task> tasks = new List<Task>();
                for (int i = 0; i < count; i++)
                {
                    //获取队列瓦片信息
                    TileID tile = tileIDs[i];
                    //用于随机访问服务端口的随机数
                    Random random = new Random();
                    int rand = random.Next(0, 8);
                    //创建启动下载所必要的信息类
                    DownLoadInfo info = new DownLoadInfo("http://t" + rand + ".tianditu.gov.cn/" +
                        imagType +
                        "_w" +
                        "/wmts?SERVICE=WMTS&REQUEST=GetTile&VERSION=1.0.0&LAYER=" +
                        imagType +
                        "&STYLE=default&TILEMATRIXSET=w&FORMAT=tiles&TILEMATRIX=" + tile.z + "&TILEROW=" + tile.y + "&TILECOL=" + tile.x + "&tk=" + token, tile.y.ToString(), tile.z.ToString(), tile.x.ToString(), i, reloadInfos, path);
                    //执行下载
                    #region 测试多线程
                    await semaphore.WaitAsync();

                    Task task = Task.Run(async () =>
                    {
                        try
                        {
                            await DownTask(info);
                        }
                        finally
                        {
                            semaphore.Release(); // 释放信号量，允许其他任务进入  
                        }
                    });

                    tasks.Add(task);

                    #endregion

                    //ThreadPool.QueueUserWorkItem(DownLoadThread, info);
                    //Down(info);
                    // 暂存信息类
                    //var callBack = new WaitCallback(DownLoadThread);
                    //ThreadPool.QueueUserWorkItem(callBack, info);
                }

                await Task.WhenAll(tasks);

                Console.ReadLine();
                Console.Clear();
                Console.WriteLine("下载完成");
                //重下载
                if (reloadInfos.Count > 0)
                {
                    Console.Clear();
                    Console.WriteLine("存在下载失败的队列是否重新下载Y/N");
                    string inputStr = Console.ReadLine();
                    if (inputStr == "Y" || inputStr == "y")
                    {
                        for (int i = 0; i < reloadInfos.Count; i++)
                        {
                            //获取队列瓦片信息
                            TileID tile = tileIDs[i];
                            //用于随机访问服务端口的随机数
                            Random random = new Random();
                            int rand = random.Next(0, 8);
                            //创建启动下载所必要的信息类
                            DownLoadInfo info = new DownLoadInfo("http://t" + rand + ".tianditu.gov.cn/cia_w/wmts?SERVICE=WMTS&REQUEST=GetTile&VERSION=1.0.0&LAYER=cia&STYLE=default&TILEMATRIXSET=w&FORMAT=tiles&TILEMATRIX=" + tile.z + "&TILEROW=" + tile.y + "&TILECOL=" + tile.x + "&tk=05c106e931ace1a6b0da2cad1c9f984a", tile.y.ToString(), tile.z.ToString(), tile.x.ToString(), i, null, path);
                            //执行下载
                            Down(info);
                        }
                    }
                    else
                    {
                        List<DownLoadRecorder> RecorderList = new List<DownLoadRecorder>();
                        foreach (var item in reloadInfos)
                        {
                            DownLoadRecorder recorder = new DownLoadRecorder(item.Url, item.Name, item.Layer, item.X, item.Index, item.Path);
                            RecorderList.Add(recorder);
                        }
                        string json = JsonConvert.SerializeObject(RecorderList);
                        File.Create(path + "\\RecorderList.json").Close();
                        File.WriteAllText(path + "\\RecorderList.json", string.Empty);
                        File.WriteAllText(path + "\\RecorderList.json", json);
                    }
                }
                #endregion
            }
            else
                return;
            #endregion
        }
        private static SemaphoreSlim semaphore = new SemaphoreSlim(16);
        private static Task GetEmptyTask(List<Task> tasks)
        {
            foreach (var item in tasks)
            {
                if (item.IsCompleted)
                    return item;
            }
            return null;
        }
        /// <summary>
        /// 获取要矩形范围内要下载的瓦片编码队列
        /// </summary>
        /// <param name="layer">要下载的层级0-18</param>
        /// <param name="leftUpLon">左上角经度</param>
        /// <param name="rightDownLat">左上角纬度</param>
        /// <param name="rightDownLon">右下角经度</param>
        /// <param name="rightDownLat">右下角纬度度</param>
        /// <returns>范围瓦片编码队列</returns>
        private static List<TileID> CaclulatTiles(int layer, double leftUpLon, double leftUpLat, double rightDownLon, double rightDownLat)
        {
            List<TileID> tileIDs = new List<TileID>();
            //根据层级遍历
            for (int i = 0; i < layer; i++)
            {
                //每个层级左上角的编码
                var leftUp = GetTileId(leftUpLon, leftUpLat, i);
                //每个层级右下角的编码
                var rightDown = GetTileId(rightDownLon, rightDownLat, i);
                for (int k = leftUp.x; k <= rightDown.x; k++)
                {
                    for (int n = leftUp.y; n <= rightDown.y; n++)
                    {
                        var tile = new TileID(i, k, n);
                        tileIDs.Add(tile);
                    }
                }
            }
            return tileIDs;
        }

        /// <summary>
        /// 用于线程池调用的回调函数
        /// </summary>
        /// <param name="obj"></param>
        public static void DownLoadThread(object obj)
        {
            var info = obj as DownLoadInfo;
            Down(info);
        }

        #region 地图下载
        /// <summary>
        /// 下载图片
        /// </summary>
        /// <param name="picUrl">图片Http地址</param>
        /// <param name="savePath">保存路径（本地）</param>
        /// <param name="timeOut">Request最大请求时间，如果为-1则无限制</param>
        /// <returns></returns>
        public static bool DownloadPicture(string picUrl, string savePath, int timeOut = -1)
        {
            //picUrl = "https://pic.cnblogs.com/avatar/1465512/20200617142308.png";
            //savePath = "D:/img/" + DateTime.Now.ToString("HHmmssffff") + ".jpg";
            bool value = false;
            WebResponse response = null;
            Stream stream = null;
            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(picUrl);
                if (timeOut != -1) request.Timeout = timeOut;
                response = request.GetResponse();
                stream = response.GetResponseStream();
                if (!response.ContentType.ToLower().StartsWith("text/"))
                    value = SaveBinaryFile(response, savePath);
            }
            finally
            {
                if (stream != null) stream.Close();
                if (response != null) response.Close();
            }
            return value;
        }
        private static bool SaveBinaryFile(WebResponse response, string savePath)
        {
            bool value = false;
            byte[] buffer = new byte[1024];
            Stream outStream = null;
            Stream inStream = null;
            try
            {
                if (File.Exists(savePath)) File.Delete(savePath);
                outStream = System.IO.File.Create(savePath);
                inStream = response.GetResponseStream();
                int l;
                do
                {
                    l = inStream.Read(buffer, 0, buffer.Length);
                    if (l > 0) outStream.Write(buffer, 0, l);
                } while (l > 0);
                value = true;
            }
            finally
            {
                if (outStream != null) outStream.Close();
                if (inStream != null) inStream.Close();
            }
            return value;
        }

        /// <summary>
        /// 执行下载并保存到本地
        /// </summary>
        /// <param name="url">图片地址</param>
        /// <param name="name">保存的文件名=瓦片y</param>
        /// <param name="layer">层级文件夹名=瓦片z</param>
        /// <param name="x">二级层级文件夹名=瓦片x</param>
        /// <param name="index">第几个</param>
        /// <param name="reloadTiles">重传队列</param>
        public static void Down(string url, string name, string layer, string x, int index, List<DownLoadInfo> reloadInfos, string downloadPath)
        {
            //创建文件夹
            CreateDirectory(downloadPath, layer);//一级文件夹
            CreateDirectory(downloadPath + layer + "\\", x);//二级文件夹
            try
            {
                WebRequest wreq = WebRequest.Create(url);
                //发送网络请求
                HttpWebResponse wresp = (HttpWebResponse)wreq.GetResponse();

                Stream s = wresp.GetResponseStream();
                System.Drawing.Image img;
                //下载图片
                img = System.Drawing.Image.FromStream(s);
                //图片另存为
                img.Save(downloadPath + "\\" + layer + "\\" + "\\" + x + "\\" + name + ".png", ImageFormat.Png);
                MemoryStream ms = new MemoryStream();
                img.Save(ms, ImageFormat.Png);
                img.Dispose();
                Console.WriteLine("完成第" + index + "个");
            }
            catch//意味着没有下载成功
            {
                var info = new DownLoadInfo(url, name, layer, x, index, reloadInfos, downloadPath);
                //存入重下载队列
                reloadInfos.Add(info);
            }
        }
        /// <summary>
        /// 通过封装好的数据类来下载的方法
        /// </summary>
        /// <param name="info">启动下载方法的数据集合类</param>
        public static void Down(DownLoadInfo info)
        {
            Down(info.Url, info.Name, info.Layer, info.X, info.Index, info.ReloadList, info.Path);
        }
        public static async Task DownTask(DownLoadInfo info)
        {
            Down(info.Url, info.Name, info.Layer, info.X, info.Index, info.ReloadList, info.Path);
        }
        /// <summary>
        /// 从指定目录中创建文件夹若已存在则退出
        /// </summary>
        /// <param name="directoryName">文件夹名</param>
        #region
        public static void CreateDirectory(string path, string newDirectoryName)
        {
            if (!Directory.Exists(path + newDirectoryName))
            {
                Directory.CreateDirectory(path + newDirectoryName);
            }
        }
        #endregion

        #endregion
        #region 地图瓦片编号
        /// <summary>
        /// 获取瓦片四叉树id的方法
        /// </summary>
        /// <param name="lon">经度</param>
        /// <param name="lat">纬度</param>
        /// <param name="layer">层级</param>
        /// <returns></returns>
        public static TileID GetTileId(double lon, double lat, int layer)
        {

            int z, x, y;
            z = layer;
            x = (int)(Math.Pow(2, z - 1) * (lon / 180 + 1));
            y = (int)(Math.Pow(2, z - 1) * (1 - (Math.Log(Math.Tan((Math.PI * lat) / 180) + (Sec(Math.PI * lat / 180)))) / Math.PI));
            TileID tileID = new TileID(z, x, y);
            //Console.WriteLine(tileID.z.ToString() + "-" + tileID.x.ToString() + "-" + tileID.y.ToString());
            return tileID;
        }
        public static double Sec(double x)
        {
            return 1 / Math.Cos(x);
        }
        public class TileID
        {
            public int z;
            public int x;
            public int y;
            public TileID(int z, int x, int y)
            {
                this.z = z;
                this.x = x;
                this.y = y;
            }
        }
        #endregion
        public async void GetStr(string str)
        {
            str += str;
            //await ForEach(str);
        }
        public void ForEach(string str)
        {
            for (int i = 0; i < 1000; i++)
            {
                Console.WriteLine(str);
            }
        }
    }
}
/// <summary>
/// 用于启动下载的数据类
/// </summary>
public class DownLoadInfo
{
    string url;
    string name;
    string layer;
    string x;
    int index;
    List<DownLoadInfo> reloadList;
    string path;
    public DownLoadInfo(string URL, string Name, string Layer, string X, int Index, List<DownLoadInfo> ReloadList, string Path)
    {
        Url = URL;
        this.Name = Name;
        this.Layer = Layer;
        this.X = X;
        this.Index = Index;
        this.ReloadList = ReloadList;
        this.Path = Path;
    }

    public string Url { get => url; set => url = value; }
    public string Name { get => name; set => name = value; }
    public string Layer { get => layer; set => layer = value; }
    public string X { get => x; set => x = value; }
    public int Index { get => index; set => index = value; }
    public List<DownLoadInfo> ReloadList { get => reloadList; set => reloadList = value; }
    public string Path { get => path; set => path = value; }
}
/// <summary>
/// 上述的信息类存在嵌套采用下面的方法避免嵌套
/// </summary>
public class DownLoadRecorder
{
    string url;
    string name;
    string layer;
    string x;
    int index;
    string path;
    public DownLoadRecorder(string URL, string Name, string Layer, string X, int Index, string Path)
    {
        Url = URL;
        this.Name = Name;
        this.Layer = Layer;
        this.X = X;
        this.Index = Index;
        this.Path = Path;
    }

    public string Url { get => url; set => url = value; }
    public string Name { get => name; set => name = value; }
    public string Layer { get => layer; set => layer = value; }
    public string X { get => x; set => x = value; }
    public int Index { get => index; set => index = value; }
    public string Path { get => path; set => path = value; }
}
