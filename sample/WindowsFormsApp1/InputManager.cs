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
        public int Duration;
        public int Frame;
        public bool State => Duration == 0;
        public bool IsDirection => '1' <= Key && Key <= '9';
        public bool HasAttribute(char c)
        {
            switch (c)
            {
                case '8':
                    return Key == '7' || Key == '8' || Key == '9';
                case '2':
                    return Key == '1' || Key == '2' || Key == '3';
                case '4':
                    return Key == '7' || Key == '4' || Key == '1';
                case '6':
                    return Key == '9' || Key == '6' || Key == '3';
            }

            throw new Exception();
        }
    }

    public class InputManager
    {
        private class DurationInfo
        {
            public int Elem8;
            public int Elem2;
            public int Elem4;
            public int Elem6;
            public int[] Buttons = new int[4];
        }

        private Joystick pad;
        private JoystickState prevState = new JoystickState();
        private DurationInfo duration = new DurationInfo();
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

        private static int ToDirection(int x, int y)
        {
            var value = 5;
            if (x > 300) value += 1;
            if (x < -300) value -= 1;
            if (y < -300) value += 3;
            if (y > 300) value -= 3;
            return value;
        }

        public void Update()
        {
            if (this.pad == null) return;

            this.pad.Acquire();
            this.pad.Poll();

            var state = this.pad.GetCurrentState();
            if (state == null) return;

            if (state.X != prevState.X || state.Y != prevState.Y)
            {
                void OnNextAxisRelease(bool cond, ref int duration, char key)
                {
                    if (!cond) return;
                    this._keyStream.OnNext(new KeyInfo
                    {
                        Key = key,
                        Duration = duration,
                        Frame = frame,
                    });
                    duration = 0;
                }

                OnNextAxisRelease(prevState.X > 300 && state.X < 300, ref this.duration.Elem6, '6');
                OnNextAxisRelease(prevState.X < -300 && state.X > -300, ref this.duration.Elem4, '4');
                OnNextAxisRelease(prevState.Y > 300 && state.Y < 300, ref this.duration.Elem8, '8');
                OnNextAxisRelease(prevState.Y < -300 && state.Y > -300, ref this.duration.Elem2, '2');

                this._keyStream.OnNext(new KeyInfo
                {
                    Key = ToDirection(state.X, state.Y).ToString()[0],
                    Duration = 0,
                    Frame = frame,
                });
            }

            if (state.X > 300) this.duration.Elem6++;
            if (state.X < -300) this.duration.Elem4++;
            if (state.Y > 300) this.duration.Elem8++;
            if (state.Y < -300) this.duration.Elem2++;

            char[] buttonName = { 'A', 'B', 'C', 'D' };
            for (int i = 0; i < 4; ++i)
            {
                if (state.Buttons[i] != this.prevState.Buttons[i])
                {
                    this._keyStream.OnNext(new KeyInfo
                    {
                        Key = buttonName[i],
                        Duration = state.Buttons[i] ? 0 : this.duration.Buttons[i],
                        Frame = this.frame,
                    });
                    this.duration.Buttons[i] = 0;
                }

                if (state.Buttons[i])
                {
                    this.duration.Buttons[i]++;
                }
            }

            this.prevState = state;
            ++this.frame;
        }
    }
}
