using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Core;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Media;
using Windows.UI;

namespace YouTube
{
 public partial class Video : Page
 {
 // Fields for element-only fullscreen
 private Popup _fullscreenPopup;
 private Panel _originalParentPanel;
 private ContentControl _originalParentContentControl;
 private UIElement _placeholder;
 private int _originalIndex = -1;
 private double? _oldWidth;
 private double? _oldHeight;
 private HorizontalAlignment _oldHAlign;
 private VerticalAlignment _oldVAlign;

 private void Video_PageLoaded(object sender, RoutedEventArgs e)
 {
 UpdateFullscreenButtonIcon();

 // Ensure back requests will exit element fullscreen first
 try
 {
 SystemNavigationManager.GetForCurrentView().BackRequested += Element_BackRequested;
 }
 catch { }
 }

 private void Element_BackRequested(object sender, BackRequestedEventArgs e)
 {
 if (_isFullScreen)
 {
 e.Handled = true;
 try
 {
 ExitElementFullScreen();
 }
 catch (Exception ex)
 {
 System.Diagnostics.Debug.WriteLine($"Element_BackRequested error: {ex.Message}");
 }
 }
 }

 private void FullscreenButton_Click(object sender, RoutedEventArgs e)
 {
 try
 {
 ToggleFullScreen();
 }
 catch (Exception ex)
 {
 System.Diagnostics.Debug.WriteLine($"FullscreenButton_Click error: {ex.Message}");
 }
 }

 private void UpdateFullscreenButtonIcon()
 {
 try
 {
 if (FullscreenButton == null) return;
 var fontIcon = FullscreenButton.Content as FontIcon;
 bool isFull = _isFullScreen;
 if (fontIcon != null)
 {
 // E740 = Enter Full Screen, E73E = Leave Full Screen
 fontIcon.Glyph = isFull ? "\uE73E" : "\uE740";
 }
 }
 catch { }
 }

 private void EnterElementFullScreen()
 {
 if (VlcMediaElement == null || _isFullScreen) return;

 try
 {
 // Save alignment and size
 _oldWidth = double.IsNaN(VlcMediaElement.Width) ? (double?)null : VlcMediaElement.Width;
 _oldHeight = double.IsNaN(VlcMediaElement.Height) ? (double?)null : VlcMediaElement.Height;
 _oldHAlign = VlcMediaElement.HorizontalAlignment;
 _oldVAlign = VlcMediaElement.VerticalAlignment;

 // Find parent
 var parent = VlcMediaElement.Parent;
 // If parent is Panel, remember index and insert placeholder
 var panel = parent as Panel;
 if (panel != null)
 {
 _originalParentPanel = panel;
 _originalIndex = panel.Children.IndexOf(VlcMediaElement);
 _placeholder = new Border { Background = new SolidColorBrush(Colors.Transparent), Width = VlcMediaElement.ActualWidth, Height = VlcMediaElement.ActualHeight };
 panel.Children.Insert(_originalIndex, _placeholder);
 panel.Children.Remove(VlcMediaElement);
 }
 else if (parent is ContentControl contentControl)
 {
 _originalParentContentControl = contentControl;
 _placeholder = new Border { Background = new SolidColorBrush(Colors.Transparent) };
 contentControl.Content = _placeholder;
 }
 else
 {
 // Fallback: try to remove from visual parent via generic approach
 try
 {
 var fe = parent as FrameworkElement;
 // Not handling other types explicitly for SDK14393 compatibility
 }
 catch { }
 }

 // Create popup and attach VlcMediaElement
 _fullscreenPopup = new Popup { IsLightDismissEnabled = false };
 var root = new Grid { Background = new SolidColorBrush(Colors.Black), HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch };

 // Make VlcMediaElement fill root
 VlcMediaElement.Width = Window.Current.Bounds.Width;
 VlcMediaElement.Height = Window.Current.Bounds.Height;
 VlcMediaElement.HorizontalAlignment = HorizontalAlignment.Stretch;
 VlcMediaElement.VerticalAlignment = VerticalAlignment.Stretch;

 root.Children.Add(VlcMediaElement);
 _fullscreenPopup.Child = root;
 _fullscreenPopup.IsOpen = true;

 _isFullScreen = true;
 UpdateFullscreenButtonIcon();
 }
 catch (Exception ex)
 {
 System.Diagnostics.Debug.WriteLine($"EnterElementFullScreen error: {ex.Message}");
 }
 }

 private void ExitElementFullScreen()
 {
 if (VlcMediaElement == null || !_isFullScreen) return;

 try
 {
 // Remove from popup and close
 if (_fullscreenPopup != null)
 {
 var root = _fullscreenPopup.Child as Panel;
 if (root != null && root.Children.Contains(VlcMediaElement))
 {
 root.Children.Remove(VlcMediaElement);
 }
 _fullscreenPopup.IsOpen = false;
 _fullscreenPopup.Child = null;
 _fullscreenPopup = null;
 }

 // Restore to original parent
 if (_originalParentPanel != null && _placeholder != null)
 {
 var index = _originalIndex;
 // Ensure valid index
 if (index <0 || index > _originalParentPanel.Children.Count) index = _originalParentPanel.Children.Count;
 // Remove placeholder and insert element at same position
 var placeholderIndex = _originalParentPanel.Children.IndexOf(_placeholder);
 if (placeholderIndex >=0)
 {
 _originalParentPanel.Children.RemoveAt(placeholderIndex);
 _originalParentPanel.Children.Insert(placeholderIndex, VlcMediaElement);
 }
 else
 {
 _originalParentPanel.Children.Insert(index, VlcMediaElement);
 }

 _originalParentPanel = null;
 }
 else if (_originalParentContentControl != null && _placeholder != null)
 {
 _originalParentContentControl.Content = VlcMediaElement;
 _originalParentContentControl = null;
 }

 // Restore size and alignment
 if (_oldWidth.HasValue)
 {
 VlcMediaElement.Width = _oldWidth.Value;
 }
 else
 {
 VlcMediaElement.ClearValue(FrameworkElement.WidthProperty);
 }

 if (_oldHeight.HasValue)
 {
 VlcMediaElement.Height = _oldHeight.Value;
 }
 else
 {
 VlcMediaElement.ClearValue(FrameworkElement.HeightProperty);
 }

 VlcMediaElement.HorizontalAlignment = _oldHAlign;
 VlcMediaElement.VerticalAlignment = _oldVAlign;

 _placeholder = null;
 _originalIndex = -1;
 _isFullScreen = false;
 UpdateFullscreenButtonIcon();
 }
 catch (Exception ex)
 {
 System.Diagnostics.Debug.WriteLine($"ExitElementFullScreen error: {ex.Message}");
 }
 }

 // ToggleFullScreen will call these methods via existing ToggleFullScreen() implementation in other partial file.
 }
}
