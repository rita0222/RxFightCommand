using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Threading;

using System.Reactive.Linq;
using System.Reactive.Disposables;
using System.Diagnostics;
using System.Reactive;

namespace WindowsFormsApp1
{
    public static class DisposableExtesntions
    {
        public static IDisposable AddTo(this IDisposable d, CompositeDisposable cd)
        {
            cd.Add(d);
            return d;
        }
    }

    public partial class GameForm : Form, IDisposable
    {
        private readonly InputManager input = new InputManager();

        private readonly CompositeDisposable _cd = new CompositeDisposable();

        private IObservable<KeyInfo> CreateCommandObserver(string command)
        {
            IObservable<KeyInfo> getInputObserver(char c)
            {
                return input.KeyStream.Where(k => k.Key == c && k.State);
            }

            bool isDirection(char c)
            {
                return '1' <= c && c <= '9';
            }

            var observer = getInputObserver(command[0]);
            for (int i = 1; i < command.Length; ++i)
            {
                var index = i;
                if (command[index] != command[index - 1])
                {
                    observer = observer
                        .Merge(getInputObserver(command[index]));
                }

                observer = observer
                    .Buffer(2, 1)
                    .Where(b => (index == 1 && isDirection(command[0]))
                             || b[1].Frame - b[0].Frame < 16)
                    .Where(b => b[0].Key == command[index - 1]
                             && b[1].Key == command[index])
                    .Select(b => b[1]);
            }

            return observer;
        }

        /// 
        /// コンストラクタ
        /// 
        public GameForm()
        {
            // デザイナ設定反映
            InitializeComponent();

            // スタイルの指定
            SetStyle(ControlStyles.AllPaintingInWmPaint |// ちらつき抑える
                ControlStyles.Opaque, true);　           // 背景は描画しない

            // 最大化を無効にする
            MaximizeBox = false;

            var hadohken = CreateCommandObserver("236A")
                .Do(_ => Debug.WriteLine("波動拳！"))
                .Subscribe().AddTo(_cd);
            var shoryuken = CreateCommandObserver("623C")
                .Do(_ => Debug.WriteLine("昇龍拳！"))
                .Subscribe().AddTo(_cd);
            var tatsumaki = CreateCommandObserver("214B")
                .Do(_ => Debug.WriteLine("竜巻旋風脚！"))
                .Subscribe().AddTo(_cd);
            var shinkuhadoh = CreateCommandObserver("236236C")
                .Do(_ => Debug.WriteLine("真空波動拳！"))
                .Subscribe().AddTo(_cd);

            var yaotome = CreateCommandObserver("2363214C")
                .Do(_ => Debug.WriteLine("遊びは終わりだ！"))
                .Subscribe().AddTo(_cd);
            var powergazer = CreateCommandObserver("21416C")
                .Do(_ => Debug.WriteLine("パワゲイザーッ！"))
                .Subscribe().AddTo(_cd);
            var rasingstorm = CreateCommandObserver("1632143C")
                .Do(_ => Debug.WriteLine("レイジングストォーム！"))
                .Subscribe().AddTo(_cd);
            var jigokugokuraku = CreateCommandObserver("6321463214C")
                .Do(_ => Debug.WriteLine("チョーシこいてんじゃねぇぞコラァ！"))
                .Subscribe().AddTo(_cd);

            var shungoku = CreateCommandObserver("AA6BC")
                .Do(_ => Debug.WriteLine("一瞬千撃！"))
                .Subscribe().AddTo(_cd);

            var hyakuretu = this.input.KeyStream
                .Where(k => k.Key == 'D' && k.State)
                .Buffer(4, 1)
                .Where(b => b[3].Frame - b[0].Frame < 30)
                .Do(_ => Debug.WriteLine("やっやっやっ"))
                .Subscribe().AddTo(_cd);

            var hatsudo = this.input.KeyStream
                .Where(k => (k.Key == 'A' || k.Key == 'B' || k.Key == 'C') && k.State)
                .Buffer(3, 1)
                .Where(b => b.Max(k => k.Frame) - b.Min(k => k.Frame) < 2)
                .Select(b => b.Select(k => k.Key))
                .Where(a => a.Contains('A') && a.Contains('B') && a.Contains('C'))
                .Do(_ => Debug.WriteLine("じゃきーん！"))
                .Subscribe().AddTo(_cd);
        }

        /// 
        /// 毎フレーム処理
        /// 
        public void Exec()
        {
            // フォームの生成
            Show();
            // フォームが作成されている間は、ループし続ける
            while (Created)
            {
                MainLoop();

                // イベントがある場合は処理する
                Application.DoEvents();

                // CPUがフル稼働しないようにFPSの制限をかける
                // ※簡易的に、おおよそ秒間60フレーム程度に制限
                Thread.Sleep(16);
            }
        }

        /// 
        /// メインループ処理
        /// 
        public void MainLoop()
        {
            this.input.Update();
        }

        /// 
        /// 解放処理
        /// 
        public new void Dispose()
        {
            base.Dispose();
        }
    }
}
