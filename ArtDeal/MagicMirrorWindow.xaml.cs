﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using IPlugins;
using Brushes = System.Windows.Media.Brushes;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using PixelFormat = System.Drawing.Imaging.PixelFormat;
using Point = System.Windows.Point;

namespace ArtDeal
{
    /// <summary>
    /// MagicMirrorWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MagicMirrorWindow : IDisposable
    {

        private readonly Bitmap _cacheFirstBitmap;
        private readonly IEnumerable<Bitmap> _cacheBitmaps;
        private Point _originPoint;
        private int _factor;
        private Bitmap _resultBitmap;
        private WriteableBitmap _writeableBitmap;
        private byte[] _bitmapBuffer;

        #region Protect methods

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var hwndSource = PresentationSource.FromVisual(this) as HwndSource;
            hwndSource?.AddHook(WndProc);
        }

        protected const int WmNchittest = 0x0084;
        protected const int AgWidth = 12;
        protected const int BThickness = 4;

        protected virtual IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            switch (msg)
            {
                case WmNchittest:
                    var mousePoint = new System.Drawing.Point(lParam.ToInt32() & 0xFFFF, lParam.ToInt32() >> 16);
                    if (mousePoint.Y - Top <= AgWidth
                       && mousePoint.X - Left <= AgWidth)
                    {
                        handled = true;
                        return new IntPtr((int)HitTest.Httopleft);
                    }
                    else if (ActualHeight + Top - mousePoint.Y <= AgWidth
                       && mousePoint.X - Left <= AgWidth)
                    {
                        handled = true;
                        return new IntPtr((int)HitTest.Htbottomleft);
                    }
                    else if (mousePoint.Y - Top <= AgWidth
                       && ActualWidth + Left - mousePoint.X <= AgWidth)
                    {
                        handled = true;
                        return new IntPtr((int)HitTest.Httopright);
                    }
                    else if (ActualWidth + Left - mousePoint.X <= AgWidth
                       && ActualHeight + Top - mousePoint.Y <= AgWidth)
                    {
                        handled = true;
                        return new IntPtr((int)HitTest.Htbottomright);
                    }
                    else if (mousePoint.X - Left <= BThickness)
                    {
                        handled = true;
                        return new IntPtr((int)HitTest.Htleft);
                    }
                    else if (ActualWidth + Left - mousePoint.X <= BThickness)
                    {
                        handled = true;
                        return new IntPtr((int)HitTest.Htright);
                    }
                    else if (mousePoint.Y - Top <= BThickness)
                    {
                        handled = true;
                        return new IntPtr((int)HitTest.Httop);
                    }
                    else if (ActualHeight + Top - mousePoint.Y <= BThickness)
                    {
                        handled = true;
                        return new IntPtr((int)HitTest.Htbottom);
                    }
                    else
                    {
                        handled = false;
                        return IntPtr.Zero;
                    }
            }
            return IntPtr.Zero;
        }

        protected enum HitTest
        {
            Hterror = -2,
            Httransparent = -1,
            Htnowhere = 0,
            Htclient = 1,
            Htcaption = 2,
            Htsysmenu = 3,
            Htgrowbox = 4,
            Htsize = Htgrowbox,
            Htmenu = 5,
            Hthscroll = 6,
            Htvscroll = 7,
            Htminbutton = 8,
            Htmaxbutton = 9,
            Htleft = 10,
            Htright = 11,
            Httop = 12,
            Httopleft = 13,
            Httopright = 14,
            Htbottom = 15,
            Htbottomleft = 16,
            Htbottomright = 17,
            Htborder = 18,
            Htreduce = Htminbutton,
            Htzoom = Htmaxbutton,
            Htsizefirst = Htleft,
            Htsizelast = Htbottomright,
            Htobject = 19,
            Htclose = 20,
            Hthelp = 21,
        }

        #endregion

        public HandleResult HandleResult { get; private set; }

        public MagicMirrorWindow(IEnumerable<Bitmap> bitmaps)
        {
            InitializeComponent();
            _cacheBitmaps = bitmaps;
            _cacheFirstBitmap = _cacheBitmaps.First();

            var screenHeight = SystemParameters.VirtualScreenHeight;
            var screenWidth = SystemParameters.VirtualScreenWidth;
            var height = _cacheFirstBitmap.Height + 125.0;
            var width = _cacheFirstBitmap.Width + 40.0;
            if (height < 300)
            {
                height = 300;
            }
            else if (height > screenHeight)
            {
                height = screenHeight;
            }
            if (width < 300)
            {
                width = 300;
            }
            else if (width > screenWidth)
            {
                width = screenWidth;
            }
            Height = height;
            Width = width;

            _factor = 100;
            _originPoint = new Point(_cacheFirstBitmap.Width / 2.0, _cacheFirstBitmap.Height / 2.0);
        }

        private void WindowLoaded(object sender, RoutedEventArgs e)
        {
            this.DisableMaxmize(true); //禁用窗口最大化功能
            this.RemoveSystemMenuItems(Win32.SystemMenuItems.Restore | Win32.SystemMenuItems.Minimize | Win32.SystemMenuItems.Maximize | Win32.SystemMenuItems.SpliteLine); //去除窗口指定的系统菜单
            //TitleLbl.Content = $"哈哈镜处理: {_factor}";
            _resultBitmap = GetHandledImage(_cacheFirstBitmap, _originPoint, _factor);
            _writeableBitmap = new WriteableBitmap(Imaging.CreateBitmapSourceFromHBitmap(_resultBitmap.GetHbitmap(), IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions()));
            TargetImage.Source = _writeableBitmap;
        }

        private void WindowKeyDown(object sender, KeyEventArgs e)
        {
            var value = (int)Slider.Value;
            if (e.Key == Key.Left)
            {
                if (value >= 1)
                {
                    Slider.Value = value - 1;
                }
            }
            else if (e.Key == Key.Right)
            {
                if (value <= 999)
                {
                    Slider.Value = value + 1;
                }
            }
        }

        private void DragMoveWindow(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void LeftBtn_Click(object sender, RoutedEventArgs e)
        {
            HandleResult = new HandleResult(null, false);
            Close();
        }

        private void RightBtn_Click(object sender, RoutedEventArgs e)
        {
            var resultBitmaps = new List<Bitmap>()
            {
                (Bitmap)_resultBitmap.Clone()
            };
            for (var i = 1; i < _cacheBitmaps.Count(); i++)
            {
                resultBitmaps.Add(GetHandledImage(_cacheBitmaps.ElementAt(i), _originPoint, _factor));
            }
            HandleResult = new HandleResult(resultBitmaps, true);
            Close();
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e)
        {
            HandleResult = new HandleResult(null, false);
            Close();
        }

        private void TargetImage_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed || _cacheFirstBitmap == null) return;
            _originPoint = e.GetPosition(TargetImage);
            _resultBitmap?.Dispose();
            _resultBitmap = GetHandledImage(_cacheFirstBitmap, _originPoint, _factor);
            UpdateImage(_resultBitmap);
        }

        private void TargetImage_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            var position = e.GetPosition(TargetImage);
            var x = (int)Math.Ceiling(position.X);
            var y = (int)Math.Ceiling(position.Y);
            if (x > 0) x--;
            if (y > 0) y--;
            TitleLbl.Content = $"哈哈镜处理: ({x},{y})";
        }

        private void TargetImage_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            TitleLbl.Content = "哈哈镜处理";
        }

        private void ExchangeBgCbx_Click(object sender, RoutedEventArgs e)
        {
            if (ExchangeBgCbx.IsChecked == true)
            {
                ImageViewGrid.Background = Brushes.White;
                ImageBorder.BorderThickness = new Thickness(0.1);
            }
            else
            {
                ImageViewGrid.Background = Brushes.Transparent;
                ImageBorder.BorderThickness = new Thickness(0);
            }
        }

        private void ExchangeBgCbx_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            var dialog = new ColorDialog();
            if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
            var color = dialog.Color;
            ImageViewGrid.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(color.A, color.R, color.G, color.B));
        }

        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_cacheFirstBitmap == null) return;
            _resultBitmap?.Dispose();
            _factor = (int)e.NewValue;
            //TitleLbl.Content = $"哈哈镜处理: {_factor}";
            _resultBitmap = GetHandledImage(_cacheFirstBitmap, _originPoint, _factor);
            UpdateImage(_resultBitmap);
        }

        private Bitmap GetHandledImage(Bitmap bitmap, Point originPoint, int factor)
        {
            var bmp = (Bitmap)bitmap.Clone();
            if (factor == 0) return bmp;
            try
            {
                var originX = (int)Math.Ceiling(originPoint.X) - 1;
                var originY = (int)Math.Ceiling(originPoint.Y) - 1;
                var width = bmp.Width;
                var height = bmp.Height;
                const int pixelSize = 4;
                
                var bmpData = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
                var byColorInfo = new byte[height * bmpData.Stride];
                Marshal.Copy(bmpData.Scan0, byColorInfo, 0, byColorInfo.Length);

                #region Safe

                //var clone = (byte[])byColorInfo.Clone();
                //for (var x = 0; x < width; x++)
                //{
                //    for (var y = 0; y < height; y++)
                //    {
                //        var dx = x - originX;
                //        var dy = y - originY;
                //        var theta = Math.Atan2(dy, dx);
                //        var mapR = Math.Sqrt(Math.Sqrt(dx * dx + dy * dy) * factor);
                //        dx = originX + (int)(mapR * Math.Cos(theta));
                //        dy = originY + (int)(mapR * Math.Sin(theta));
                //        if (dx < 0 || dx >= width || dy < 0 || dy >= height)
                //        {
                //            byColorInfo[y * bmpData.Stride + x * pixelSize + 3] = 0;
                //        }
                //        else
                //        {
                //            byColorInfo[y * bmpData.Stride + x * pixelSize] = clone[dy * bmpData.Stride + dx * 4];
                //            byColorInfo[y * bmpData.Stride + x * pixelSize + 1] = clone[dy * bmpData.Stride + dx * 4 + 1];
                //            byColorInfo[y * bmpData.Stride + x * pixelSize + 2] = clone[dy * bmpData.Stride + dx * 4 + 2];
                //        }
                //    }
                //}
                //Marshal.Copy(byColorInfo, 0, bmpData.Scan0, byColorInfo.Length);


                #endregion

                #region Unsafe

                unsafe
                {
                   
                    fixed (byte* source = byColorInfo)
                    {
                        var ptr = (byte*)(bmpData.Scan0);
                        for (var y = 0; y < height; y++)
                        {
                            for (var x = 0; x < width; x++)
                            {
                                var dx = x - originX;
                                var dy = y - originY;
                                var theta = Math.Atan2(dy, dx);
                                var mapR = Math.Sqrt(Math.Sqrt(dx * dx + dy * dy) * factor);
                                
                                dx = originX + (int)(mapR * Math.Cos(theta));
                                dy = originY + (int)(mapR * Math.Sin(theta));
                                
                                if (dx < 0 || dx >= width || dy < 0 || dy >= height)
                                {
                                    ptr[0] = ptr[1] = ptr[2] = ptr[3] = 0;
                                }
                                else
                                {
                                    ptr[0] = source[dy * bmpData.Stride + dx * pixelSize];
                                    ptr[1] = source[dy * bmpData.Stride + dx * pixelSize + 1];
                                    ptr[2] = source[dy * bmpData.Stride + dx * pixelSize + 2];
                                    ptr[3] = source[dy * bmpData.Stride + dx * pixelSize + 3];
                                }
                                ptr += pixelSize;
                            }
                        }
                    }
                }

                #endregion

                bmp.UnlockBits(bmpData);
                return bmp;
            }
            catch (Exception e)
            {
                HandleResult = new HandleResult(e);
                Close();
                return (Bitmap)bitmap.Clone();
            }
            
        }

        private void UpdateImage(Bitmap bitmap)
        {
            _writeableBitmap.Lock();
            var bmpData = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
            if (_bitmapBuffer == null)
            {
                _bitmapBuffer = new byte[bitmap.Height * bmpData.Stride];
            }
            Marshal.Copy(bmpData.Scan0, _bitmapBuffer, 0, _bitmapBuffer.Length);
            Marshal.Copy(_bitmapBuffer, 0, _writeableBitmap.BackBuffer, _bitmapBuffer.Length);
            bitmap.UnlockBits(bmpData);
            _writeableBitmap.AddDirtyRect(new Int32Rect(0, 0, _writeableBitmap.PixelWidth, _writeableBitmap.PixelHeight));
            _writeableBitmap.Unlock();
        }

        public void Dispose()
        {
            _resultBitmap?.Dispose();
        }

        
    }
}
