﻿using ClosedXML.Excel;
using Microsoft.Extensions.FileProviders;
using Mintea.HtmlToDom;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Text;

namespace Mintea.RazorHelper
{
    /// <summary>
    /// Razorによるコード生成で使用した関数
    /// </summary>
    public class RazorHelper
    {
        // TODO:Extensionにしたのでそちらに移行する
        #region InputDynamic:動的にdynamic型を生成する
        /// <summary>
        /// 動的にdynamic型を生成する
        /// </summary>
        /// <param name="Fields">フィールド名とそのオブジェクト(このメソッドで生成したdynamicでも良い)の組み合わせ</param>
        /// <returns></returns>
        public static dynamic InputDynamic(Dictionary<string, object> Fields)
        {
            dynamic result = new ExpandoObject();
            IDictionary<string, object> work = result;
            foreach (var item in Fields) { work.Add(item.Key, item.Value); }

            return result;
        }
        #endregion

        #region MakeSequence:生成するシートの順番を作成する（子シート優先にする）
        /// <summary>
        /// 生成するシートの順番を作成する
        /// 子シート優先にする
        /// </summary>
        /// <param name="excel">Excelデータ</param>
        /// <param name="errors">エラーを格納するところ</param>
        /// <returns>シートの順番が書かれたList</returns>
        public static List<string> MakeSequence(Dictionary<string, List<List<string>>> excel, List<string> errors)
        {
            // 親リスト作成
            var parentList = MakeParentList(excel, errors);

            // 名前リスト作成の為の意味のないリスト
            // （本来は木構造に持たせるデータを入れることができるが、今回は順番が欲しいだけなので名前だけ持たせる）
            var nameList = new Dictionary<string, string>();
            foreach (var item in excel)
            {
                nameList.Add(item.Key, item.Key);
            }

            // 木構造作成
            var tree = TreeNode<string>.MakeTree(new TreeNode<string>(string.Empty), nameList, parentList);

            // 深さ優先探索
            var result = new List<string>();
            var resultTreeList = tree.DepthList();

            // 名前を取り出す
            foreach (var item in resultTreeList)
            {
                if (item.Value != string.Empty)
                {
                    result.Add(item.Value);
                }
            }

            return result;
        }
        #endregion

        #region GetIndex:シート内の指定した列の番号を取得する
        /// <summary>
        /// シート内の指定した列の番号を取得する
        /// </summary>
        /// <param name="sheet"></param>
        /// <returns>なかったら-1</returns>
        public static int GetIndex(List<List<string>> sheet, string name)
        {
            var result = -1;

            if (sheet.Count > 2)
            {
                for (int i = 0; i < sheet[0].Count; i++)
                {
                    if (sheet[0][i] == name)
                    {
                        return i;
                    }
                }
            }

            return result;
        }
        #endregion

        #region MakeParentList:親リスト作成
        /// <summary>
        /// Excelから親リストを作成する
        /// ある子要素は、親無しまたは1種類の親を持つ前提のデータ構造である
        /// 親要素はシート内のParent列で判断する
        /// </summary>
        /// <param name="excel"></param>
        /// <param name="errors"></param>
        /// <returns>各要素の親がどの要素かを挙げたリスト</returns>
        public static Dictionary<string, string> MakeParentList(Dictionary<string, List<List<string>>> excel, List<string> errors)
        {
            var parentList = new Dictionary<string, string>();

            foreach (var sheetName in excel.Keys)
            {
                // リスト
                var sheet = excel[sheetName];

                // Parentの列番号を取得
                var parentIndex = GetIndex(sheet, "Parent");

                // Parentがある場合
                if (parentIndex >= 0)
                {
                    for (int i = 2; i < sheet.Count; i++)
                    {
                        if (!sheet[i][parentIndex].Contains("."))
                        {
                            errors.Add($"Parentに'.'が入ってない。sheet:{sheetName} row:{i} column:{parentIndex} value:{sheet[i][parentIndex]}");
                        }
                        else
                        {
                            // 子情報を登録する
                            var splited = sheet[i][parentIndex].Split('.');

                            if (parentList.Keys.Contains(sheetName))
                            {
                                if (parentList[sheetName] != splited[0])
                                {
                                    errors.Add($"同じParent列に違う親が書かれている。sheet:{sheetName} row:{i} column:{parentIndex} value:{sheet[i][parentIndex]}");
                                }
                            }
                            else
                            {
                                parentList.Add(sheetName, splited[0]);
                            }
                        }
                    }
                }
            }
            return parentList;
        }
        #endregion

        #region MakeChildData:子データ作成
        /// <summary>
        /// 子データを先に作っておきます
        /// 現在の所、どの親にどの子がぶら下がるかだけ。
        /// キー情報などは載せてない。
        /// </summary>
        /// <param name="excel"></param>
        /// <param name="errors"></param>
        /// <returns>0:親Sheet名、1:子Sheet名重複なし</returns>
        public static Dictionary<string, List<string>> MakeChildData(Dictionary<string, List<List<string>>> excel, List<string> errors)
        {
            var childList = new Dictionary<string, List<string>>();

            foreach (var sheetName in excel.Keys)
            {
                if (sheetName.EndsWith("List"))
                {
                    // リスト
                    var sheet = excel[sheetName];
                    var parentIndex = GetIndex(sheet, "Parent");
                    if (sheet.Count > 2)
                    {
                        // Parentがある場合
                        if (parentIndex > 0)
                        {
                            for (int i = 2; i < sheet.Count; i++)
                            {
                                if (!sheet[i][parentIndex].Contains("."))
                                {
                                    errors.Add($"Parentに'.'が入ってない。sheet:{sheetName} row:{i} column:{parentIndex} value:{sheet[i][parentIndex]}");
                                }
                                else
                                {
                                    // 子情報を登録する
                                    var splited = sheet[i][parentIndex].Split('.');

                                    if (!childList.Keys.Contains(splited[0]))
                                    {
                                        childList.Add(splited[0], new List<string>());
                                    }
                                    if (!childList[splited[0]].Contains(splited[1]))
                                    {
                                        if (!childList[splited[0]].Contains(sheetName))
                                        {
                                            // 重複して同じ名前が入らないように。（キーも格納する場合は別だが。）
                                            childList[splited[0]].Add(sheetName);
                                        }
                                        //childList[splited[0]].Add(splited[1]);  // 今回はキーまで載せない。
                                    }
                                }
                            }
                        }
                    }
                }
            }
            return childList;
        }
        #endregion

        #region 子シートのdynamicデータ格納・保持
        /// <summary>
        /// 子シートのデータを親のキー別にdynamic化したものを格納して保持、親dynamic生成時にここから取得する。
        /// Listではない。既にdynamicでまとまったデータを格納している。1つの親キーに1件のみ。
        /// </summary>
        /// <param name="children">子データを格納する配列</param>
        /// <param name="parentName">親の名前</param>
        /// <param name="parentKey">親のキー</param>
        /// <param name="childSheetName">子の名前( = 親のフィールド名)</param>
        public static void AddChildrenData(Dictionary<string, Dictionary<string, Dictionary<string, dynamic>>> children, string parentName, string parentKey, string childSheetName, dynamic data)
        {
            if (!children.ContainsKey(parentName))
            {
                children.Add(parentName, new Dictionary<string, Dictionary<string, dynamic>>());
            }
            if (!children[parentName].ContainsKey(parentKey))
            {
                children[parentName].Add(parentKey, new Dictionary<string, dynamic>());
            }
            if (!children[parentName][parentKey].ContainsKey(childSheetName))
            {
                children[parentName][parentKey].Add(childSheetName, new Dictionary<string, dynamic>());
            }
            children[parentName][parentKey][childSheetName] = data;
        }
        #endregion

        #region CreateModel:Razorに入力するModelを作成する
        /// <summary>
        /// Razorに入力するModelを作成する
        /// </summary>
        /// <returns></returns>
        public static dynamic CreateModel(Dictionary<string, List<List<string>>> excel)
        {
            var errors = new List<string>();

            // データ作成順を決める
            var sequence = MakeSequence(excel, errors);

            // 親を持たない各シートのデータ：キーはシート名
            var topDataList = new Dictionary<string, dynamic>();

            // 子情報取得
            var childList = MakeChildData(excel, errors);

            // 子シートデータ
            var childDynamic = new Dictionary<string, Dictionary<string, Dictionary<string, dynamic>>>();

            // 子シートから順番にデータ作成
            foreach (var sheetName in sequence)
            {
                // 1つのシート
                var sheet = excel[sheetName];

                // 親があるか
                var parentIndex = GetIndex(sheet, "Parent");

                // 親シート名
                var parentName = string.Empty;

                // キー取得
                var keyIndex = GetIndex(sheet, "Key");

                if (sheetName.StartsWith("Doc"))
                {
                    // なにもなし
                    continue;
                }
                else if (sheetName.EndsWith("List"))
                {
                    // リスト
                    if (sheet.Count > 2)
                    {
                        // 親Key別データ、親が無い場合はシート全体のデータ（親キー、1行データ）
                        var dataByParent = new Dictionary<string, List<dynamic>>();

                        // 2行目まで読まない
                        for (int row = 2; row < sheet.Count; row++)
                        {
                            // 1行読む
                            var parentKey = "-1";   // 対象の親のKey
                            var rowData = new Dictionary<string, object>();
                            for (int col = 0; col < sheet[row].Count; col++)
                            {
                                if (col == parentIndex)
                                {
                                    // 親参照はdynamicデータに登録しないが、子dynamicデータリストに保持させるための情報を取得する
                                    var split = sheet[row][col].Split('.');
                                    parentName = split[0];
                                    parentKey = split[1];
                                }
                                else if (col == keyIndex)
                                {
                                    // 子を追加
                                    var key = sheet[row][col];  // 書かれているKeyを取得
                                    AddChildDynamic(childList, childDynamic, sheetName, key, rowData);
                                }
                                else
                                {
                                    // ParentでもKeyでもない通常の列
                                    rowData.Add(sheet[0][col], sheet[row][col]);
                                }
                            }

                            // 行データをdynamic化し、親Key別のリストに追加する。
                            // 親がないシートは必ず1件の同じリストに入る
                            if (!dataByParent.ContainsKey(parentKey))
                            {
                                dataByParent.Add(parentKey, new List<dynamic>());
                            }
                            dataByParent[parentKey].Add(InputDynamic(rowData));
                        }

                        // 親Key別のリストをどこかに登録する。
                        // 親がないシートは必ず1件
                        foreach (var dataByParentKey in dataByParent.Keys)
                        {
                            if (parentIndex >= 0)
                            {
                                // 親がある場合は、データを溜めておく
                                AddChildrenData(childDynamic, parentName, dataByParentKey, sheetName, dataByParent[dataByParentKey]);
                            }
                            else
                            {
                                // 親が無いシートはトップにデータを入れる
                                topDataList.Add(sheetName, dataByParent[dataByParentKey]);
                            }
                        }
                    }
                }
                else
                {
                    // 今回、通常シートは親子関係を持たない。持たせたい場合はListシートと同様に実装すればできるはず。
                    // …というか、リストの下位互換かも。通常シート不要説。
                    // 通常シート：1列目が名前、2列目が値
                    if (sheet.Count > 2)
                    {
                        var data = new Dictionary<string, object>();
                        // 2行目まで読まない
                        for (int row = 2; row < sheet.Count; row++)
                        {
                            var name = sheet[row][0];
                            var value = sheet[row][1];
                            data.Add(name, value);
                        }
                        // 親がないのでトップデータリストに追加
                        topDataList.Add(sheetName, InputDynamic(data));
                    }
                }
            }
            var result = InputDynamic(topDataList);

            return result;
        }
        #endregion

        #region AddChildDynamic:行データに子データを追加
        /// <summary>
        /// 行データに子データを追加
        /// </summary>
        /// <param name="childList">子情報</param>
        /// <param name="children">子シートデータ</param>
        /// <param name="sheetName">親シート名</param>
        /// <param name="key">親キー</param>
        /// <param name="rowData">追加対象行データ</param>
        public static void AddChildDynamic(Dictionary<string, List<string>> childList, Dictionary<string, Dictionary<string, Dictionary<string, dynamic>>> children, string sheetName, string key, Dictionary<string, object> rowData)
        {
            // 子を追加
            // Key列があるListシートの場合、子dynamicデータを探して保持する。
            if (childList.ContainsKey(sheetName))
            {
                var childNames = childList[sheetName];
                foreach (var childName in childNames)
                {
                    if (children.ContainsKey(sheetName) && children[sheetName].ContainsKey(key) && children[sheetName][key].ContainsKey(childName))
                    {
                        rowData.Add(childName, children[sheetName][key][childName]);
                    }
                    else
                    {
                        // 子情報があるのにキーに対する子データはなかった場合は、空データを作っておくべき。
                        rowData.Add(childName, new List<string>());
                    }
                }
            }
        }
        #endregion

        #region ReadExcel:Excelファイルを読み込む
        /// <summary>
        /// Excelファイルを読み込み、シート名をキーとした辞書にする
        /// xlsxのみ対応
        /// </summary>
        /// <param name="directry">ディレクトリ</param>
        /// <param name="filename">拡張子付きのファイル名</param>
        /// <param name="isRequiredTitle">1行目に何もない列を無視する</param>
        /// <returns>シート名をキーとした辞書、行と列の2次元string</returns>
        public static Dictionary<string, List<List<string>>> ReadExcel(string directry, string filename, bool isRequiredTitle = false)
        {
            // ファイルの読み込み
            var xlsx = new Dictionary<string, List<List<string>>>();
            using (PhysicalFileProvider provider = new PhysicalFileProvider(directry))
            {
                // ファイル情報を取得
                IFileInfo fileInfo = provider.GetFileInfo(filename);

                // ファイル存在チェック
                if (fileInfo.Exists)
                {
                    using (var wb = new XLWorkbook(fileInfo.PhysicalPath))
                    {
                        foreach (var ws in wb.Worksheets)
                        {
                            // ワークシート
                            List<List<string>> sheet = new List<List<string>>();
                            // TODO:何も書いてないシートがあると落ちる
                            for (int i = 1; i <= ws.LastCellUsed().Address.RowNumber; i++)
                            {
                                List<string> raw = new List<string>();
                                for (int j = 1; j <= ws.LastCellUsed().Address.ColumnNumber; j++)
                                {
                                    // 1行目に何もない列を無視する
                                    if (!isRequiredTitle || !string.IsNullOrWhiteSpace(ws.Cell(1, j).Value.ToString()))
                                    {
                                        raw.Add(ws.Cell(i, j).Value.ToString());
                                    }
                                }
                                sheet.Add(raw);
                            }

                            // シート名と一緒に登録
                            xlsx.Add(ws.Name, sheet);
                        }
                    }
                }

                return xlsx;
            }
        }
        #endregion

        #region SafeCreateDirectory
        /// <summary>
        /// 指定したパスにディレクトリが存在しない場合
        /// すべてのディレクトリとサブディレクトリを作成します
        /// </summary>
        /// <param name="directory"></param>
        /// <returns></returns>
        public static DirectoryInfo SafeCreateDirectory(string directory)
        {
            if (!directory.EndsWith("\\") && !directory.EndsWith("/"))
            {
                directory += "/";
            }
            if (Directory.Exists(directory))
            {
                return null;
            }
            return Directory.CreateDirectory(directory);
        }
        #endregion
    }
}
