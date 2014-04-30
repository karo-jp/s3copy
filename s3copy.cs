using System;
using System.IO;
using System.Collections.Generic;

using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Transfer;
using System.Configuration;
using System.Net;

namespace s3copy
{
    public class s3copy : s3base
    {
        public List<string> srcFiles = new List<string>();
        public Options option;

        public s3copy()
        {
        }


        /// <summary>
        /// 再帰的にCopy対象ファイルのリストを作る。
        /// </summary>
        /// <param name="s3copy"></param>
        /// <param name="dir"></param>
        private void recAddFiles( string dir)
        {
            string[] files = Directory.GetFiles(dir, "*.*");
            List<string> listFiles = new List<string>(files);

            if (option.NewerThanDateTime == DateTime.MinValue)
                srcFiles.AddRange(listFiles);
            else
            {
                listFiles.ForEach(fn =>
                    {
                        FileInfo fi = new FileInfo(fn);
                        if (fi.LastWriteTime > option.NewerThanDateTime)
                            srcFiles.Add(fn);
                    }
                );
            }

            var dirs = Directory.GetDirectories(dir);
            foreach (var f in dirs)
            {
                recAddFiles(f);
            }
        }



        /// <summary>
        /// ディレクトリ丸ごとコピー
        /// </summary>
        /// <param name="directory"></param>
        private void CopyDirectory( string directory )
        {
            var request = new TransferUtilityUploadDirectoryRequest
            {
                BucketName = this.Bucket,
                Directory = directory,
                KeyPrefix = procDirectoryName(directory)
                 
            };
            if ( !OnlyCount ) transferUtility.UploadDirectory(request);
            Console.WriteLine("[{0}]", directory);
        }

        /// <summary>
        /// 指定パスのファイルをS3にコピーする
        /// </summary>
        /// <param name="filename"></param>
        private void CopyFile(string filename, int max, int index)
        {
            // S3上での名前
            var filekey = procDirectoryName( filename );
            var fileinfo = new FileInfo(filename);
            DateTime lastModified = DateTime.MaxValue;

            var response = findS3(filekey);
            if (null != response)
                lastModified = response.LastModified;

            // 全コピーモードか、S3上にファイルが無いか、S3上のファイルより新しい場合にアップロード
            if (option.RunMode == Options.Mode.All || null == response || fileinfo.LastWriteTimeUtc > lastModified)
            {
                var request = new TransferUtilityUploadRequest
                {
                    BucketName = this.Bucket,
                    FilePath = filename,
                    Key = filekey,

                };
                if (!OnlyCount)
                {
                    transferUtility.Upload(request);
                }
                Console.WriteLine("[{1:###,###}/{2:###,###}] Copied...{0}", filename, index, max);
                CopiedFiles.Add(filename);
            }
            else
            {
                Console.WriteLine("[{1:###,###}/{2:###,###}] Skip...{0}", filename, index, max);
            }
        }

        /// <summary>
        /// 対象のファイルを削除する
        /// </summary>
        /// <param name="targetkey"></param>
        private void DeleteFile(string targetkey)
        {
            try
            {
                var req = new DeleteObjectRequest { BucketName = Bucket, Key = targetkey };
                var response = client.DeleteObject(req);
                //WebHeaderCollection headers = response.Headers;
                //foreach (string key in headers.Keys)
                //{
                //    Console.WriteLine("Response Header: {0}, Value: {1}", key, headers[key]);
                //}
                Console.WriteLine("Deleted. \"{0}\"", targetkey);
            }
            catch (AmazonS3Exception amazonS3Exception)
            {
                if (amazonS3Exception.ErrorCode != null &&
                    (amazonS3Exception.ErrorCode.Equals("InvalidAccessKeyId") ||
                    amazonS3Exception.ErrorCode.Equals("InvalidSecurity")))
                {
                    Console.WriteLine("Please check the provided AWS Credentials.");
                    Console.WriteLine("If you haven't signed up for Amazon S3, please visit http://aws.amazon.com/s3");
                }
                else
                {
                    Console.WriteLine("An error occurred with the message '{0}' when deleting an object", amazonS3Exception.Message);
                }
            }

        }

        public override void Prepare()
        {
            // 対象ファイルを収集
            recAddFiles(TargetDirectory);

            // トリガーファイルを一番おしりに。
            if (!string.IsNullOrEmpty(option.triggerFile))
            {
                FileInfo fi = new FileInfo(Path.Combine(TargetDirectory, option.triggerFile));
                var found = srcFiles.Find(m => m.Equals(fi.FullName, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrEmpty(found))
                {
                    srcFiles.Remove(found);
                    srcFiles.Add(found);
                }
            }


        }

        /// <summary>
        /// srcFilesに配置されたファイルを、S3上のファイルとタイムスタンプを比較して新しければコピーする
        /// </summary>
        public override void Run()
        {
            if (string.IsNullOrEmpty(Bucket))
                throw new Exception("バケット名が指定されていません。");

            // トリガーモードでS3が新しい、または同じだったら何もしない
            if (triggerState == TriggerState.S3 || triggerState == TriggerState.Same)
            {
                Console.WriteLine("Trigger is not updated. abort execution.");
                return;
            }

            var response =  transferUtility.S3Client.ListBuckets();
            if (null == response.Buckets.Find(b => b.BucketName == Bucket.ToLower()))
            {
                
                PutBucketRequest pbrec = new PutBucketRequest { UseClientRegion = true, BucketName = Bucket.ToLower() };
                transferUtility.S3Client.PutBucket(pbrec);
            }

            /// S3にのみ含まれるファイルを削除
            var targets = FindDeletedFiles();
            foreach (var t in targets)
            {
                DeleteFile(t);
            }

            var index = 0;

            foreach (var filepath in srcFiles)
            {
                try
                {
                    index++;
                    if (File.Exists(filepath))
                    {
                        CopyFile(filepath, srcFiles.Count, index);
                    }
                    else if (Directory.Exists(filepath))
                        CopyDirectory(filepath);
                }
                catch (System.IO.IOException ex)
                {
                    Console.Write("[{2}/{3}] IO Error.{1} skip \"{0}\"", filepath, ex.Message, index, srcFiles.Count);
                }
                catch (Exception ex)
                {
                    Console.Write("[{2}/{3}] {1}. skip \"{0}\"", filepath, ex.Message, index, srcFiles.Count);
                }


            }
        }

        private void uploadFileProgressCallback(object sender, UploadProgressArgs e)
        {
            Console.WriteLine("\x1b[2A[{0}/{1}]", e.TransferredBytes, e.TotalBytes);
            //updateProgressBar(this._ctlFileProgressBar, 0, e.TotalBytes, e.TransferredBytes,
           //     this._ctlFileTransferLabel, "Bytes", null);
        }


        /// <summary>
        /// S3バケットのみに含まれるファイルを検出
        /// </summary>
        /// <returns></returns>
        public List<string> FindDeletedFiles()
        {
            var ret = new List<string>();

            CopiedFiles.Clear();

            if (string.IsNullOrEmpty(Bucket))
                throw new Exception("バケット名が指定されていません。");

            ListObjectsResponse response = new ListObjectsResponse();

            do
            {
                // S3バケット内のオブジェクト取得リクエストを構築
                ListObjectsRequest request = new ListObjectsRequest
                {
                    BucketName = Bucket,
                    MaxKeys = int.MaxValue,         // 取れるだけを要求するが、実際に取れるのは1000件
                    Marker = response.NextMarker,   // 取得開始カーソル
                };

                response = transferUtility.S3Client.ListObjects(request);
                foreach (var o in response.S3Objects)
                {
                    // S3のキー名から対応するローカルファイル名を生成
                    var filename = Path.Combine(TargetDirectory,procKey(o.Key));
                    var dirname = Path.GetDirectoryName(filename);

                    if (!Directory.Exists(dirname) || // 存在しないディレクトリ
                        string.IsNullOrEmpty(Path.GetFileName(filename)) || // ファイル名が無い
                        !File.Exists(filename)) // 存在しない
                    {
                        ret.Add(o.Key);
                    }
                }
            } while (!string.IsNullOrEmpty(response.NextMarker));

            return ret;

        }
    }
}
