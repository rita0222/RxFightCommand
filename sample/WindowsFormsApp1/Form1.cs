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

        private Subject<int> _direction = new Subject<int>();

        private Subject<bool>[] _buttons = new Subject<bool>[4];

        private CompositeDisposable _cd = new CompositeDisposable();

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
                _buttons[i] = new Subject<bool>();
            }

            var subscribers = new[]
            {
                _direction.Subscribe(d => Debug.WriteLine(d)),
                _buttons[0].Subscribe(d => Debug.WriteLine("[0] " + d)),
                _buttons[1].Subscribe(d => Debug.WriteLine("[1] " + d)),
                _buttons[2].Subscribe(d => Debug.WriteLine("[2] " + d)),
                _buttons[3].Subscribe(d => Debug.WriteLine("[3] " + d)),
            };
            foreach(var subscriber in subscribers)
            {
                _cd.Add(subscriber);
            }

            _direction
                .Buffer(TimeSpan.FromMilliseconds(500), 4)
                .Where(p => p.Count >= 3 && p[0] == 2 && p[1] == 3 && p[2] == 6)
                .Do(_ => Debug.WriteLine("レバーOK"))
                .Zip(_buttons[2]
                        .Where(b => b)
                        .Do(_ => Debug.WriteLine("ボタンOK")),
                        (_, __) => Unit.Default)
                .Subscribe(_ => Debug.WriteLine("波動拳！"));
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
                _direction.OnNext(value);
            }

            for (int i = 0; i < 4; ++i)
            {
                if (jState.Buttons[i] != _prevState.Buttons[i])
                {
                    _buttons[i].OnNext(jState.Buttons[i]);
                }
            }

            _prevState = jState;

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
