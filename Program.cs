using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;


namespace s3copy
{
    class Program
    {
//        private static string [] args = { "-all","-new" , "-retrieve", "-count" };

        /// <summary>
        /// 起動オプションをパースする
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        private static Options ParseOptions(string[] args)
        {
            Options ret = new Options();
            if (!CommandLine.Parser.Default.ParseArguments(args, ret))
            {
                throw new Exception("コマンドラインの解析に失敗しました。");
            }

            // ターゲットローカルディレクトリが指定されていない場合はカレントディレクトリとする
            if ( null == ret.targetDirectory || ret.targetDirectory.Count == 0 )
            {
                ret.targetDirectory = new List<string>();
                ret.targetDirectory.Add(Directory.GetCurrentDirectory());
            }
            // バケット
            if ( string.IsNullOrWhiteSpace(ret.bucketName))
            {
                ret.bucketName = ConfigurationManager.AppSettings["Bucket"];
            }

            return ret;
        }

        private static void Usage()
        {
            // Usage なんか
            Console.WriteLine("s3coy [--bucket bucketname] [--trigger triggerfilename] [--mode all|new|retrieve] [--count] [targetdirectory]");
            Console.WriteLine("Bucket名の指定が省略された場合はconfigから取得します。");
            Console.WriteLine("targetdirectoryは省略可能(カレントディレクトリ)。");
            Console.WriteLine("s3copy -all でカレントディレクトリ以下を全部S3にコピー。");
            Console.WriteLine("s3copy -new で、S3上のファイルより新しい場合にS3にコピー。");
            Console.WriteLine("s3copy -retrieve で、カレントディレクトリにファイル復元。");
            Console.WriteLine("ファイルのタイムスタンプが古いものは上書きされます。");
            Console.WriteLine("-trigger で、トリガーファイルを指定できます。指定された場合、トリガーファイルがアップデートされない限り、コピーまたは復元は実行されません。");
            Console.WriteLine("-count スイッチを追加すると、処理対象のファイル数だけ数えます。");
        }

        public static int Main(string[] args)
        {
            Options opt = ParseOptions(args);

                try
                {
                    if (opt.RunMode == Options.Mode.None)
                        throw new Exception("起動スイッチが指定されていません。-all, -new, -retrieve のいずれかを指定してください。");

                    TimeExecuter tx = new TimeExecuter
                    { 
                        options = opt,

                    };

                    int ret =  tx.Run();

                    return ret;
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    Usage();
                }
                return 0;
        }
    }
}