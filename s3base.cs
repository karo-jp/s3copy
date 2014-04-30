using System;
using System.Configuration;
using System.Collections.Generic;
using System.Text;
using Amazon.S3.Model;
using Amazon.S3.Transfer;
using Amazon.S3;
using System.IO;
using Amazon;

namespace s3copy
{
    /// <summary>
    /// S3コンテナを使う為のベースクラス。デフォルトで東京リージョンを向いている。
    /// </summary>
    public class s3base
    {
        private RegionEndpoint _region = null;
        public RegionEndpoint Region
        {
            get
            {
                if ( null == _region )
                {
                    if (!string.IsNullOrWhiteSpace(ConfigurationManager.AppSettings["EndPoint"]))
                        _region = RegionEndpoint.GetBySystemName(ConfigurationManager.AppSettings["EndPoint"]);
                }
                return _region;
            }
            set
            {
                _region = value;
            }
        }

        public List<string> CopiedFiles = new List<string>();

        public enum TriggerState
        {
            None,
            Same,
            S3,
            Local
        }

        public TriggerState triggerState = TriggerState.None;

        private string _backet = null;
        public bool OnlyCount = false;

        public string Bucket { 
            get {
                if (string.IsNullOrEmpty(_backet))
                {
                    _backet = ConfigurationManager.AppSettings["Bucket"];
                }
                return _backet;

            }
            set {
                _backet = value;
            }
        }
        public int timeOut = 1800;
        public string TargetDirectory;

        public string ServiceURL = "s3-ap-northeast-1.amazonaws.com";
                   

        private TransferUtility _transferUtility = null;
        private IAmazonS3 _client = null;

        public IAmazonS3 client
        {
            get
            {
                if (null == _client)
                    _client = AWSClientFactory.CreateAmazonS3Client(Region);
                return _client;

            }
        }
        public TransferUtility transferUtility  {
            get
            {
                if (null == _transferUtility)
                {
                    _transferUtility = new TransferUtility(client);
                }
                return _transferUtility;
            }
        }


        /// <summary>
        /// パスをBucket上の相対キーにする
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        protected string procDirectoryName(string path)
        {
            var di = new DirectoryInfo(path);

            //フルパスから、開始ディレクトリより上位のディレクトリ要素を削除する
            var ret = path.Replace(TargetDirectory, "");
            ret = ret.Replace("\\", "/");
            if (ret.Length > 0 && ret[0] == '/')
                ret = ret.Substring(1);

            return ret;
        }


        /// <summary>
        /// S3上に配置された指定キーのファイルメタ情報を取得する。
        /// ファイルが存在しない場合はNULLを返す。
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        protected GetObjectMetadataResponse findS3(string key)
        {
            var request = new GetObjectMetadataRequest
            {
                BucketName = Bucket.ToLower(),
                Key = key,
            };

            GetObjectMetadataResponse ret = null;


            try
            {
                var response = transferUtility.S3Client.GetObjectMetadata(request);
                ret = response;
            }
            catch
            {
                ret = null;
            }
            return ret;

        }

        /// <summary>
        /// S3キー値を対応するファイル名に変換する。
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        protected string procKey(string key)
        {
            return key.Replace("/", "\\");
        }
        

        /// <summary>
        /// トリガーファイルのタイムスタンプからトリガーの状態を判別、設定する。
        /// </summary>
        /// <param name="triggerFile">トリガーファイル名</param>
        /// <returns>トリガー状態</returns>
        public TriggerState SetTriggerState(string triggerFile)
        {
            // 指定されたファイル名にベースパスを連結、ファイルの存在確認
            FileInfo fi = new FileInfo(Path.Combine(TargetDirectory,  triggerFile));
            if (fi.Exists)
            {
                // ローカルのトリガーファイルに対応したS3上のトリガーファイルを取得
                var key = procDirectoryName(fi.FullName);
                var om = findS3(key);
                // S3トリガーファイルが存在するならローカルトリガーとタイムスタンプを比較
                if (null != om)
                {
                    if (om.LastModified == fi.LastAccessTimeUtc)
                        triggerState = TriggerState.Same;
                    else 
                        triggerState = om.LastModified <= fi.LastWriteTimeUtc ? TriggerState.Local : TriggerState.S3;
                }
                else
                {
                    // ローカルのみに存在するのでローカル側優先とする
                    triggerState = TriggerState.Local;
                }
            }
            else
            {
                // ローカルに指定されたトリガーファイルが存在しないなら例外
                throw new Exception( string.Format("トリガーファイル[{0}]が存在しません。", triggerFile));
            }
            return triggerState;        }

        public virtual void Run()
        {
        }

        public virtual void Prepare()
        {
        }

    }
}
