using System;
using System.IO;
using System.Collections.Generic;
using System.Text;

namespace s3copy
{
    public class TimeExecuter
    {

        public enum ExecutePattern {
            All,
            New
        }

        public Options options;

        /// <summary>
        /// 実行
        /// </summary>
        public int Run()
        {
            s3base runner = null;
            
            if (options.RunMode == Options.Mode.Retrieve)
            {
                runner = new s3retrieve();
            }
            else
            {
                runner = new s3copy { option = options };
            }

            // トリガーファイルが指定されていた場合、トリガーファイルのアップデート状況を確認する。
            if (!string.IsNullOrEmpty(options.triggerFile))
            {
                runner.SetTriggerState(options.triggerFile);
            }

            foreach (var dir in options.targetDirectory)
            {
                runner.TargetDirectory = dir;
                runner.OnlyCount = options.OnlyCount;
                runner.Bucket = options.bucketName;
                runner.Prepare();
                runner.Run();
            }

            return runner.CopiedFiles.Count;
        }


        /// <summary>
        /// 再帰的にCopy対象ファイルのリストを作る。
        /// </summary>
        /// <param name="s3copy"></param>
        /// <param name="dir"></param>
        private void recAddFiles(s3copy s3copy, string dir)
        {
            string[] files = Directory.GetFiles(dir, "*.*");
            List<string> listFiles = new List<string>(files);

            s3copy.srcFiles.AddRange(listFiles);

            var dirs = Directory.GetDirectories(dir);
            foreach (var f in dirs)
            {
                recAddFiles(s3copy, f);
            }
        }

    }
}
