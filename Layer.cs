﻿using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RT.Util.Xml;
using WpfCrutches;
using D = System.Drawing;

/// A linked "duplicate" layer (before or after effects)

namespace TankIconMaker
{
    abstract class LayerBase : IHasTreeViewItem, IHasTypeNameDescription, IXmlClassifyProcess, INotifyPropertyChanged
    {
        /// <summary>Describes what this layer type does as concisely as possible.</summary>
        [Browsable(false)]
        public abstract string TypeName { get; }
        /// <summary>Describes what this layer type does in more detail.</summary>
        [Browsable(false)]
        public abstract string TypeDescription { get; }
        /// <summary>Specifies the version of this layer’s settings - incremented on changing layer settings backwards-incompatibly.</summary>
        [Browsable(false)]
        public abstract int Version { get; }

        [Browsable(false)]
        public string Name { get { return _Name; } set { _Name = value; NotifyPropertyChanged("Name"); NotifyPropertyChanged("NameVisibility"); } }
        private string _Name;

        [Browsable(false)]
        public Visibility NameVisibility { get { return string.IsNullOrEmpty(Name) ? Visibility.Collapsed : Visibility.Visible; } }

        [Browsable(false)]
        public ObservableCollection<EffectBase> Effects { get; set; }

        [Category("General")]
        [Description("Allows you to hide this layer without deleting it.")]
        public bool Visible { get; set; }

        public LayerBase()
        {
            Effects = new ObservableCollection<EffectBase>();
            Effects.CollectionChanged += updateEffectLayer;
            Visible = true;
        }

        private void updateEffectLayer(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Add || e.Action == NotifyCollectionChangedAction.Replace)
                foreach (var item in e.NewItems.OfType<EffectBase>())
                    item.Layer = this;
            else if (e.Action == NotifyCollectionChangedAction.Reset)
                foreach (var item in Effects)
                    item.Layer = this;
        }

        /// <summary>Used internally to draw the layer. Hidden from IntelliSense to avoid confusion.</summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public abstract WriteableBitmap DrawInternal(Tank tank);

        /// <summary>
        /// Stores the <see cref="Version"/> of the maker as it was at the time of saving settings to XML. This may
        /// then be used to apply transformations to the XML produced by old versions of a maker.
        /// </summary>
        private int SavedByVersion;
        void IXmlClassifyProcess.BeforeXmlClassify() { SavedByVersion = Version; }
        void IXmlClassifyProcess.AfterXmlDeclassify()
        {
            foreach (var effect in Effects)
                effect.Layer = this;
            Effects.CollectionChanged -= updateEffectLayer;
            Effects.CollectionChanged += updateEffectLayer;
        }

        [XmlIgnore, Browsable(false)]
        public TreeViewItem TreeViewItem { get; set; }

        protected void NotifyPropertyChanged(string name) { PropertyChanged(this, new PropertyChangedEventArgs(name)); }
        public event PropertyChangedEventHandler PropertyChanged = (_, __) => { };

        public LayerBase Clone()
        {
            var result = MemberwiseClone() as LayerBase;
            result.PropertyChanged = (_, __) => { };
            result.Effects = new ObservableCollection<EffectBase>();
            result.Effects.CollectionChanged += result.updateEffectLayer;
            foreach (var e in Effects)
                result.Effects.Add(e.Clone());
            return result;
        }
    }

    abstract class LayerBaseWpf : LayerBase
    {
        /// <summary>
        /// This is the method that should draw the image for the specified tank. The image is always 80x24 pixels large. The maker
        /// may report errors to the user by throwing <see cref="MakerUserErrors"/>, but any other exceptions are treated as bugs.
        /// Will be invoked on multiple threads, so make sure to "Freeze" any shared resources such as brushes and images
        /// (these can be initialized by overriding <see cref="Initialize"/>).
        /// </summary>
        public abstract void Draw(Tank tank, DrawingContext dc);

        /// <summary>Used internally to draw a tank. Hidden from IntelliSense to avoid confusion.</summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public override WriteableBitmap DrawInternal(Tank tank)
        {
            var visual = new DrawingVisual();
            using (var context = visual.RenderOpen())
                Draw(tank, context);
            var bitmap = new RenderTargetBitmap(80, 24, 96, 96, PixelFormats.Pbgra32);
            RenderOptions.SetBitmapScalingMode(visual, BitmapScalingMode.HighQuality);
            bitmap.Render(visual);
            bitmap.Freeze();
            return bitmap.ToWpfWriteable();
        }
    }

    abstract class LayerBaseGdi : LayerBase
    {
        /// <summary>
        /// This is the method that should draw the image for the specified tank. The image is always 80x24 pixels large. The maker
        /// may report errors to the user by throwing <see cref="MakerUserErrors"/>, but any other exceptions are treated as bugs.
        /// Will be invoked on multiple threads, so make sure to avoid using the same resource (such as image) for several tanks.
        /// </summary>
        public abstract void Draw(Tank tank, D.Graphics dc);

        /// <summary>Used internally to draw a tank. Hidden from IntelliSense to avoid confusion.</summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public override WriteableBitmap DrawInternal(Tank tank)
        {
            var result = Ut.NewBitmapGdi();
            using (var g = D.Graphics.FromImage(result.Bitmap))
                Draw(tank, g);
            return result.ToWpfWriteable();
        }
    }

}
