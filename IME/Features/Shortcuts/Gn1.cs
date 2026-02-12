using Android.Content;
using Android.Graphics;
using Android.Media;
using Android.OS;
using Android.Util;
using Android.Views;
using Android.Views.InputMethods;
using IME.Features.Input;
using IME.Shared.Utils.FileOperation;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace IME.Features.Shortcuts
{
    public class Gn1 : LinearLayout
    {
        //当前分类
        private string _currentCategory;
        private LinearLayout _layoutLeft;
        private LinearLayout _layoutCenter;
        private Button _button_right;
        private Ime _imeService;

        public Gn1(Context context) : base(context)
        {
            Initialize(context);
        }

        public Gn1(Context context, IAttributeSet attrs) : base(context, attrs)
        {
            Initialize(context);
        }

        private void Initialize(Context context)
        {
            var inflater = (LayoutInflater)context.GetSystemService(Context.LayoutInflaterService);
            inflater.Inflate(Resource.Layout.gn1, this, true);
            GetViews();
            OnButtonClick();
        }

        //设置按钮点击事件
        public void OnButtonClick()
        {
            _button_right.Click += (_, e) =>
            {
                if (_imeService == null)
                {
                    return;
                }

                if (string.IsNullOrWhiteSpace(_currentCategory))
                {
                    var categories = _imeService._swordDBHelper.QueryCategories();
                    if (categories.Count > 0)
                    {
                        _currentCategory = categories[0];
                    }
                }

                if (string.IsNullOrWhiteSpace(_currentCategory))
                {
                    Toast.MakeText(Context, "??????", ToastLength.Short).Show();
                    return;
                }

                List<string> list = _imeService._swordDBHelper.QueryRandomItemsByCategory(_currentCategory, 1);
                if (list.Count == 0)
                {
                    Toast.MakeText(Context, "????????", ToastLength.Short).Show();
                    return;
                }

                SendMessageEvent(list[0]);
            };
        }
        
        private void GetViews()
        {
            _layoutLeft = FindViewById<LinearLayout>(Resource.Id.layout_left);
            _layoutCenter = FindViewById<LinearLayout>(Resource.Id.layout_center);
            _button_right = FindViewById<Button>(Resource.Id.button_right);
        }

        
        
        // 更新视图
        public void UpdateViews()
        {
            _layoutLeft.RemoveAllViews();
            _layoutCenter.RemoveAllViews();
            List<string> categories = _imeService._swordDBHelper.QueryCategories();
            
            foreach (var category in categories)
            {
                AddCategoryView(category);
            }

            if (categories.Count == 0)
            {
                _currentCategory = string.Empty;
            }
            else if (string.IsNullOrWhiteSpace(_currentCategory) || !categories.Contains(_currentCategory))
            {
                _currentCategory = categories[0];
            }
        }

        // 添加分类视图
        private void AddCategoryView(string category)
        {
            TextView textView = CreateTextView(category);
            textView.SetTextColor(Color.Black);
            textView.SetBackgroundColor(Color.Lavender);
            _layoutLeft.AddView(textView);

            textView.Click += async (_, e) =>
            {
                
                _layoutCenter.RemoveAllViews();
                
                var childCount = _layoutCenter.ChildCount;
                if (childCount > 0)
                {
                    //删除视图
                    _layoutCenter.RemoveAllViews();
                }
                
                Log.Info("IME", $"点击了分类：{category}");
                
                //
                _currentCategory = category;
                
                // 显示加载状态
                var progressBar = new ProgressBar(Context) {Indeterminate = true};
                _layoutCenter.AddView(progressBar);

                // 异步查询分类下的项目
                List<string> items = await Task.Run(() => _imeService._swordDBHelper.QueryRandomItemsByCategory(category, 200));
                // 移除加载状态
                _layoutCenter.RemoveView(progressBar);

                // // 添加项目视图
                // foreach (var item in items)
                // {
                //     AddItemView(item);
                // }
                // 分批次渲染（每批10个，间隔50ms）
                var batchSize = 100;
                var handler = new Handler(Looper.MainLooper);
                var counter = 0;

                Action addBatch = null;
                addBatch = () =>
                {
                    var remaining = items.Count - counter;
                    var currentBatchSize = Math.Min(batchSize, remaining);

                    for (int i = 0; i < currentBatchSize; i++)
                    {
                        var item = items[counter + i];
                        AddItemView(item); // 保持原有方法
                    }

                    counter += currentBatchSize;

                    if (counter < items.Count)
                    {
                        handler.PostDelayed(addBatch, 20); // 下一批延迟
                    }
                };

                // 启动第一批
                handler.Post(addBatch);
                
            };
        }
        
        //添加词汇视图
        private void AddItemView(string item)
        {
            TextView textView = CreateTextView(item);
            // textView.SetTextColor(Color.Aqua);
            textView.SetBackgroundColor(Color.White);
            textView.SetTextColor(Color.Black);
            _layoutCenter.AddView(textView);
            

            textView.Click += (_, e) =>
            {
                Log.Info("IME", $"点击了项目：{item}");
                SendMessageEvent(item);
            };
        }
        
        // 发送消息事件
        private void SendMessageEvent(string message)
        {
            _imeService.CommitText(message, false);
            _imeService.CurrentInputConnection.FinishComposingText();
            
            var now = Java.Lang.JavaSystem.CurrentTimeMillis();
            // var downEvent = new KeyEvent(now - 100, now, KeyEventActions.Down, Keycode.Enter, 0);
            // _imeService.CurrentInputConnection.SendKeyEvent(downEvent);
            
            new Handler(Looper.MainLooper).PostDelayed(() => 
            {
                _imeService.CurrentInputConnection.PerformEditorAction(ImeAction.Send);
            }, 50);
        }

        // 创建文本视图
        private TextView CreateTextView(string text)
        {
            return new TextView(Context)
            {
                TextSize = 20,
                Text = text,
                LayoutParameters = new LinearLayout.LayoutParams(
                    ViewGroup.LayoutParams.MatchParent, 
                    ViewGroup.LayoutParams.WrapContent)
                {
                    TopMargin = 10,
                },
                //设置文本居中
                Gravity = GravityFlags.CenterHorizontal,
            };
        }

        public void SetImeService(Ime imeService)
        {
            _imeService = imeService;
        }
    }
}
