using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Avalonia.Threading;
using System;
using System.Collections;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using tg_engine_launcher.ViewModels;

namespace tg_engine_launcher.Views.custom
{
    public partial class AutoScrollListBox : ListBox, IStyleable 
    {
        Type IStyleable.StyleKey => typeof(ListBox);
        public AutoScrollListBox()
        {
            InitializeComponent();
        }
        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        protected override void ItemsCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            base.ItemsCollectionChanged(sender, e);

            var needScroll = ((loggerVM)DataContext).NeedScroll;
            if (needScroll)
            {
                Dispatcher.UIThread.InvokeAsync(() =>
                {
                    ScrollIntoView(ItemCount - 1);
                });
            }
        }
    }
}
