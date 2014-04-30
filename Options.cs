using System;
using System.Collections.Generic;
using System.Text;

namespace s3copy
{
    public class Options
    {
        public enum Mode
        {
            None,
            New,
            All,
            Retrieve,
        }

        /// <summary>
        /// 動作も―ド
        /// New : タイムスタンプが新しいファイルのみ Local > S3
        /// All : タイムスタンプを無視してすべてのファイルを Local > S3
        /// Retrieve : タイムスタンプのより新しいファイルのみ S3 > Local
        /// </summary>
        [CommandLine.Option("mode", DefaultValue = Mode.None)]
        public Mode RunMode { get; set; }

        /// <summary>
        /// 数を数えるのみのモード
        /// </summary>
        [CommandLine.Option("count")]
        public bool OnlyCount { get; set; }

        /// <summary>
        /// 対象となるローカルディレクトリ
        /// </summary>
        [CommandLine.ValueList(typeof(List<string>))]
        public List<string> targetDirectory { get; set; }

        /// <summary>
        /// S3バケット名
        /// </summary>
        [CommandLine.Option("bucket")]
        public string bucketName { get; set; }

        /// <summary>
        /// トリガーファイル名
        /// </summary>
        [CommandLine.Option("trigger")]
        public string triggerFile { get; set; }

        /// <summary>
        /// 指定日時（より新しいファイルのみコピー対象）
        /// </summary>
        [CommandLine.Option('d',"date")]
        public DateTime NewerThanDateTime { get; set; }

    }
}
