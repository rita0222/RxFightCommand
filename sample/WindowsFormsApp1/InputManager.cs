using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reactive.Subjects;
using SharpDX.DirectInput;
using System.Reactive.Linq;

namespace WindowsFormsApp1
{
    public struct KeyInfo
    {
        public char Key;
        public bool State;
        public int Frame;
    }

    public class InputManager
    {
        private Joystick pad;
        private JoystickState prevState = new JoystickState();
        private int frame = 0;

        public InputManager()
        {
            DirectInput dinput = new DirectInput();

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

            // 見つからなかった場合
            if (joystickGuid == Guid.Empty)
            {
                return;
            }

            // パッド入力周りの初期化
            pad = new Joystick(dinput, joystickGuid);
            // バッファサイズを指定
            pad.Properties.BufferSize = 128;

            // 相対軸・絶対軸の最小値と最大値を
            // 指定した値の範囲に設定する
            foreach (DeviceObjectInstance deviceObject in pad.GetObjects())
            {
                switch (deviceObject.ObjectId.Flags)
                {
                    case DeviceObjectTypeFlags.Axis:
                    // 絶対軸or相対軸
                    case DeviceObjectTypeFlags.AbsoluteAxis:
                    // 絶対軸
                    case DeviceObjectTypeFlags.RelativeAxis:
                        // 相対軸
                        var ir = pad.GetObjectPropertiesById(deviceObject.ObjectId);
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

        private readonly Subject<KeyInfo> _keyStream = new Subject<KeyInfo>();
        public IObservable<KeyInfo> KeyStream => this._keyStream.AsObservable();

        public void Update()
        {
            if (this.pad == null) return;

            this.pad.Acquire();
            this.pad.Poll();

            var state = this.pad.GetCurrentState();
            if (state == null) return;

            if (state.X != prevState.X || state.Y != prevState.Y)
            {
                var value = 5;
                if (state.X > 300) value += 1;
                if (state.X < -300) value -= 1;
                if (state.Y < -300) value += 3;
                if (state.Y > 300) value -= 3;
                this._keyStream.OnNext(new KeyInfo
                {
                    Key = value.ToString()[0],
                    State = true,
                    Frame = frame,
                });
            }

            char[] buttonName = { 'A', 'B', 'C', 'D' };
            for (int i = 0; i < 4; ++i)
            {
                if (state.Buttons[i] != this.prevState.Buttons[i])
                {
                    this._keyStream.OnNext(new KeyInfo
                    {
                        Key = buttonName[i],
                        State = state.Buttons[i],
                        Frame = this.frame,
                    });
                }
            }

            this.prevState = state;
            ++this.frame;
        }
    }
}
