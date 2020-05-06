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

using SharpDX.DirectInput;
using System.Reactive.Linq;
using System.Reactive.Subjects;
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

    struct KeyInfo
    {
        public char Key;
        public bool State;
        public int Frame;
        public override string ToString() => Key.ToString();
    }

    /// 
    /// オリジナルのウィンドウフォームを生成する為のクラス
    /// 
    public partial class GameForm : Form, IDisposable
    {
        /// 
        /// ゲームパッド取得用変数
        /// 
        private Joystick _joy;

        private JoystickState _prevState = new JoystickState();

        private Subject<KeyInfo> _direction = new Subject<KeyInfo>();

        private Subject<KeyInfo>[] _buttons = new Subject<KeyInfo>[4];

        private CompositeDisposable _cd = new CompositeDisposable();

        private int frame = 0;

        private IObservable<KeyInfo> CreateCommandObserver(string command)
        {
            IObservable<KeyInfo> inputObserverSelector(char c)
            {
                IObservable<KeyInfo> result = null;
                switch (c)
                {
                    case '1':
                    case '2':
                    case '3':
                    case '4':
                    case '5':
                    case '6':
                    case '7':
                    case '8':
                    case '9':
                        result = _direction;
                        break;
                    case 'A':
                        result = _buttons[0];
                        break;
                    case 'B':
                        result = _buttons[1];
                        break;
                    case 'C':
                        result = _buttons[2];
                        break;
                    case 'D':
                        result = _buttons[3];
                        break;
                }

                return result.Where(d => d.Key == c && d.State);
            }

            var observer = inputObserverSelector(command[0]);
            for (int i = 1; i < command.Length; ++i)
            {
                var index = i;
                observer = observer
                    .Merge(inputObserverSelector(command[index]))
                    .Buffer(2, 1)
                    .Where(b => i == 1 ? true : b[1].Frame - b[0].Frame < 16)
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

            for (int i = 0; i < 4; ++i)
            {
                _buttons[i] = new Subject<KeyInfo>();
            }

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
        }

        /// 
        /// 毎フレーム処理
        /// 
        public void Exec()
        {
            Initialize();

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
        /// DirectXデバイスの初期化
        /// 
        public void Initialize()
        {
            // 入力周りの初期化
            DirectInput dinput = new DirectInput();
            if (dinput != null)
            {
                // 使用するゲームパッドのID
                var joystickGuid = Guid.Empty;
                // ゲームパッドからゲームパッドを取得する
                if (joystickGuid == Guid.Empty)
                {
                    foreach (DeviceInstance device in dinput.GetDevices(DeviceType.Gamepad, DeviceEnumerationFlags.AllDevices))
                    {
                        joystickGuid = device.InstanceGuid;
                        break;
                    }
                }
                // ジョイスティックからゲームパッドを取得する
                if (joystickGuid == Guid.Empty)
                {
                    foreach (DeviceInstance device in dinput.GetDevices(DeviceType.Joystick, DeviceEnumerationFlags.AllDevices))
                    {
                        joystickGuid = device.InstanceGuid;
                        break;
                    }
                }
                // 見つかった場合
                if (joystickGuid != Guid.Empty)
                {
                    // パッド入力周りの初期化
                    _joy = new Joystick(dinput, joystickGuid);
                    if (_joy != null)
                    {
                        // バッファサイズを指定
                        _joy.Properties.BufferSize = 128;

                        // 相対軸・絶対軸の最小値と最大値を
                        // 指定した値の範囲に設定する
                        foreach (DeviceObjectInstance deviceObject in _joy.GetObjects())
                        {
                            switch (deviceObject.ObjectId.Flags)
                            {
                                case DeviceObjectTypeFlags.Axis:
                                // 絶対軸or相対軸
                                case DeviceObjectTypeFlags.AbsoluteAxis:
                                // 絶対軸
                                case DeviceObjectTypeFlags.RelativeAxis:
                                    // 相対軸
                                    var ir = _joy.GetObjectPropertiesById(deviceObject.ObjectId);
                                    if (ir != null)
                                    {
                                        try
                                        {
                                            ir.Range = new InputRange(-1000, 1000);
                                        }
                                        catch (Exception) { }
                                    }
                                    break;
                            }
                        }
                    }
                }
            }
        }

        /// 
        /// メインループ処理
        /// 
        public void MainLoop()
        {
            UpdateForPad();
        }

        /// 
        /// 解放処理
        /// 
        public new void Dispose()
        {
            base.Dispose();
        }

        /// 
        /// パッド入力処理
        /// 
        public void UpdateForPad()
        {
            // フォームにフォーカスが無い場合、処理終了
            if (!Focused) { return; }
            // 初期化が出来ていない場合、処理終了
            if (_joy == null) { return; }

            // キャプチャするデバイスを取得
            _joy.Acquire();
            _joy.Poll();

            // ゲームパッドのデータ取得
            var jState = _joy.GetCurrentState();
            // 取得できない場合、処理終了
            if (jState == null) { return; }

            if (jState.X != _prevState.X || jState.Y != _prevState.Y)
            {
                var value = 5;
                if (jState.X > 300) value += 1;
                if (jState.X < -300) value -= 1;
                if (jState.Y < -300) value += 3;
                if (jState.Y > 300) value -= 3;
                _direction.OnNext(new KeyInfo
                {
                    Key = value.ToString()[0],
                    State = true,
                    Frame = frame,
                });
            }

            var buttonName = new[] { 'A', 'B', 'C', 'D' };
            for (int i = 0; i < 4; ++i)
            {
                if (jState.Buttons[i] != _prevState.Buttons[i])
                {
                    _buttons[i].OnNext(new KeyInfo
                    {
                        Key = buttonName[i],
                        State = jState.Buttons[i],
                        Frame = frame,
                    });
                }
            }

            _prevState = jState;
            ++frame;

            // 以下の処理は挙動確認用

            // 挙動確認用：押されたキーをタイトルバーに表示する
            // アナログスティックの左右軸
            bool inputX = true;
            if (jState.X > 300)
            {
                Text = "入力キー：→";
            }
            else if (jState.X < -300)
            {
                Text = "入力キー：←";
            }
            else
            {
                inputX = false;
            }
            // アナログスティックの上下軸
            bool inputY = true;
            if (jState.Y > 300)
            {
                Text = "入力キー：↓";
            }
            else if (jState.Y < -300)
            {
                Text = "入力キー：↑";
            }
            else
            {
                inputY = false;
            }
            // 未入力だった場合
            if (!inputX && !inputY)
            {
                Text = "入力キー：";
            }
        }
    }
}
