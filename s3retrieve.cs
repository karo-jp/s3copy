using System;
using System.IO;
using System.Collections.Generic;
using System.Text;

using Amazon.S3.Model;
using Amazon.S3.Transfer;


namespace s3copy
{
    public class s3retrieve : s3base
    {
        private void retrieveFile(S3Object o, string filename)
        {
            DateTime lastModified = o.LastModified;
            FileInfo fi = null;

            if (File.Exists(filename))
            {
                fi = new FileInfo(filename);
                if (fi.LastWriteTime < lastModified)
                {
                    if ( !OnlyCount ) transferUtility.Download(filename, Bucket, o.Key);
                    Console.WriteLine("Update.. {0}", filename);
                    CopiedFiles.Add(filename);
                }
                else
                {
                   // Console.WriteLine("Skip.... {0}", filename);
                }
            }
            else
            {
                if (!OnlyCount)
                {
                    transferUtility.Download(filename, Bucket, o.Key);
                }
                Console.WriteLine("Create.. {0}", filename);
                CopiedFiles.Add(filename);
            }

            /// カウントモードでなければ、タイムスタンプを再設定する
            fi = new FileInfo(filename);
            if ( lastModified != DateTime.MinValue && !OnlyCount )
                fi.LastWriteTime = lastModified;

        }

        /// <summary>
        /// 設定されたバケットからカレントディレクトリにすべてのファイルを同期する。
        /// </summary>
        /// <param name="timeFilter"></param>
        public override void Run()
        {
            // トリガーモードで、ローカルのトリガーが新しいなら何もしない
            if (triggerState == TriggerState.Local || triggerState == TriggerState.Same)
            {
                Console.WriteLine("Trigger is not updated. abort execution.");
                return;
            }

            CopiedFiles.Clear();

            if (string.IsNullOrEmpty(Bucket))
                throw new Exception("バケット名が指定されていません。");

            ListObjectsResponse response = new ListObjectsResponse();

            do
            {
                ListObjectsRequest request = new ListObjectsRequest
                {
                    BucketName = Bucket,
                    MaxKeys = int.MaxValue,
                     Marker = response.NextMarker,
                };

                response = transferUtility.S3Client.ListObjects(request);
                //            response.
                foreach (var o in response.S3Objects)
                {
                    var filename = procKey(o.Key);
                    filename = Path.Combine(TargetDirectory, filename);
                    var dirname = Path.GetDirectoryName(filename);
                    if (!Directory.Exists(dirname))
                    {
                        Directory.CreateDirectory(dirname);
                    }
                    if (!string.IsNullOrEmpty(Path.GetFileName(filename)))
                    {
                        retrieveFile(o, filename);
                    }
                }
            } while (!string.IsNullOrEmpty(response.NextMarker));
        }
    }
}
