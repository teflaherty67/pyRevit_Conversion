using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using MessageBox = System.Windows.MessageBox;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using Visibility = System.Windows.Visibility;

namespace pyRevit_Conversion
{
    /// <summary>
    /// Interaction logic for frmSelectFromList.xaml
    /// </summary>
    public partial class frmSelectFromList : Window
    {
        private ObservableCollection<SelectableItem> _allItems;
        private ObservableCollection<SelectableItem> _filteredItems;
        private bool _isRegexMode = false;
        private SelectFromListConfig _config;

        // Add these for sheet filtering
        private List<ViewSheet> _allSheets;
        private List<ViewSheetSet> _viewSheetSets;

        public List<object> SelectedItems { get; private set; }
        public SelectFromListResult Result { get; private set; }

        public frmSelectFromList()
        {
            InitializeComponent();
            _filteredItems = new ObservableCollection<SelectableItem>();
            list_lb.ItemsSource = _filteredItems;

            // Setup search textbox event
            search_tb.TextChanged += Search_TextChanged;
            search_tb.KeyDown += Search_KeyDown;
        }

        #region Static Show Methods

        /// <summary>
        /// Simple show method that returns selected items
        /// </summary>
        public static List<T> Show<T>(
            IEnumerable<T> items,
            Func<T, string> displayNameSelector,
            string title = "Select Items",
            string buttonText = "Select")
        {
            var config = new SelectFromListConfig
            {
                Title = title,
                ButtonText = buttonText
            };

            var result = ShowWithResult(items, displayNameSelector, config);
            return result?.SelectedItems?.Cast<T>().ToList() ?? new List<T>();
        }

        /// <summary>
        /// Advanced show method that returns extended result
        /// </summary>
        public static SelectFromListResult ShowWithResult<T>(
            IEnumerable<T> items,
            Func<T, string> displayNameSelector,
            SelectFromListConfig config)
        {
            var form = new frmSelectFromList();
            form.Initialize(items, displayNameSelector, config);

            var dialogResult = form.ShowDialog();

            if (dialogResult == true)
            {
                return form.Result;
            }

            return null;
        }

        #endregion

        #region Initialization

        private void Initialize<T>(IEnumerable<T> items, Func<T, string> displayNameSelector, SelectFromListConfig config)
        {
            _config = config;

            // Set window properties
            Title = config.Title;
            select_b.Content = config.ButtonText;

            // Store ViewSheets and ViewSheetSets if this is a sheet selection
            if (typeof(T) == typeof(ViewSheet))
            {
                _allSheets = items.Cast<ViewSheet>().ToList();
                _viewSheetSets = config.ViewSheetSets ?? new List<ViewSheetSet>();
            }

            // Setup items
            _allItems = new ObservableCollection<SelectableItem>(
                items.Select(item => new SelectableItem
                {
                    Item = item,
                    DisplayName = displayNameSelector(item),
                    IsSelected = config.DefaultSelectAll
                })
            );

            // Configure UI visibility
            ConfigureUI();

            // Initialize filtered list
            UpdateFilteredList();
        }

        private void ConfigureUI()
        {
            // Show/hide search
            if (!_config.ShowSearch)
            {
                search_tb.Visibility = Visibility.Collapsed;
                regexToggle_b.Visibility = Visibility.Collapsed;
            }

            // Show/hide check buttons
            if (!_config.ShowCheckButtons)
            {
                checkboxbuttons_g.Visibility = Visibility.Collapsed;
            }

            // Show/hide sheet sets dropdown
            if (_config.ShowSheetSets && _config.SheetSetOptions?.Any() == true)
            {
                ctx_groups_dock.Visibility = Visibility.Visible;
                ctx_groups_selector_cb.ItemsSource = _config.SheetSetOptions;
                ctx_groups_selector_cb.SelectedItem = _config.DefaultSheetSet ?? _config.SheetSetOptions.First();

                // Wire up the selection changed event for sheet filtering
                ctx_groups_selector_cb.SelectionChanged += OnSheetSetSelectionChanged;
            }
            else if (ctx_groups_dock != null)
            {
                ctx_groups_dock.Visibility = Visibility.Collapsed;
            }

            // Show/hide reset button
            if (_config.ShowResetButton)
            {
                reset_b.Visibility = Visibility.Visible;
            }
        }

        #endregion

        #region Search and Filtering

        private void Search_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateFilteredList();
            UpdateClearButtonVisibility();
        }

        private void Search_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                button_select(sender, e);
            }
        }

        private void UpdateFilteredList()
        {
            var searchText = search_tb.Text;

            _filteredItems.Clear();

            if (string.IsNullOrWhiteSpace(searchText))
            {
                foreach (var item in _allItems)
                {
                    _filteredItems.Add(item);
                }
            }
            else
            {
                var filteredItems = _isRegexMode ?
                    FilterByRegex(searchText) :
                    FilterByFuzzy(searchText);

                foreach (var item in filteredItems)
                {
                    _filteredItems.Add(item);
                }
            }
        }

        private IEnumerable<SelectableItem> FilterByFuzzy(string searchText)
        {
            var lowerSearchText = searchText.ToLower();
            return _allItems.Where(item =>
                item.DisplayName.ToLower().Contains(lowerSearchText));
        }

        private IEnumerable<SelectableItem> FilterByRegex(string pattern)
        {
            try
            {
                var regex = new Regex(pattern, RegexOptions.IgnoreCase);
                return _allItems.Where(item => regex.IsMatch(item.DisplayName));
            }
            catch (ArgumentException)
            {
                // Invalid regex pattern, return all items
                return _allItems;
            }
        }

        private void UpdateClearButtonVisibility()
        {
            clrsearch_b.Visibility = string.IsNullOrEmpty(search_tb.Text) ?
                Visibility.Collapsed : Visibility.Visible;
        }

        // Add this new event handler for sheet set filtering
        private void OnSheetSetSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_allSheets == null || _viewSheetSets == null) return;

            var selectedSheetSet = ctx_groups_selector_cb.SelectedItem?.ToString();
            if (string.IsNullOrEmpty(selectedSheetSet)) return;

            // Filter sheets based on selected sheet set
            List<ViewSheet> filteredSheets;

            if (selectedSheetSet == "All Sheets")
            {
                filteredSheets = _allSheets;
            }
            else
            {
                // Find the ViewSheetSet that matches the selected name
                var targetSheetSet = _viewSheetSets.FirstOrDefault(vss => vss.Name == selectedSheetSet);
                if (targetSheetSet != null)
                {
                    var sheetIdsInSet = targetSheetSet.Views.Cast<ViewSheet>().Select(v => v.Id).ToHashSet();
                    filteredSheets = _allSheets.Where(sheet => sheetIdsInSet.Contains(sheet.Id)).ToList();
                }
                else
                {
                    filteredSheets = _allSheets; // Fallback
                }
            }

            // Update the items collection with filtered sheets
            _allItems.Clear();
            foreach (var sheet in filteredSheets)
            {
                _allItems.Add(new SelectableItem
                {
                    Item = sheet,
                    DisplayName = $"{sheet.SheetNumber} - {sheet.Name}",
                    IsSelected = _config.DefaultSelectAll
                });
            }

            // Update the filtered list display
            UpdateFilteredList();
        }

        #endregion

        #region Button Events

        private void toggle_regex(object sender, RoutedEventArgs e)
        {
            _isRegexMode = regexToggle_b.IsChecked == true;

            // Update button content
            regexToggle_b.Content = _isRegexMode ?
                FindResource("regexIcon") :
                FindResource("filterIcon");

            // Update tooltip
            regexToggle_b.ToolTip = _isRegexMode ?
                "Switch to fuzzy filtering" :
                "Switch to regular expression filtering";

            UpdateFilteredList();
        }

        private void clear_search(object sender, RoutedEventArgs e)
        {
            search_tb.Text = "";
            search_tb.Focus();
        }

        private void check_all(object sender, RoutedEventArgs e)
        {
            foreach (var item in _filteredItems)
            {
                item.IsSelected = true;
            }
        }

        private void uncheck_all(object sender, RoutedEventArgs e)
        {
            foreach (var item in _filteredItems)
            {
                item.IsSelected = false;
            }
        }

        private void toggle_all(object sender, RoutedEventArgs e)
        {
            foreach (var item in _filteredItems)
            {
                item.IsSelected = !item.IsSelected;
            }
        }

        private void button_reset(object sender, RoutedEventArgs e)
        {
            foreach (var item in _allItems)
            {
                item.IsSelected = _config.DefaultSelectAll;
            }

            search_tb.Text = "";

            if (_config.ShowSheetSets && _config.SheetSetOptions?.Any() == true)
            {
                ctx_groups_selector_cb.SelectedItem = _config.DefaultSheetSet ?? _config.SheetSetOptions.First();
            }
        }

        private void button_select(object sender, RoutedEventArgs e)
        {
            var selectedItems = _allItems.Where(item => item.IsSelected).Select(item => item.Item).ToList();

            if (!selectedItems.Any() && _config.RequireSelection)
            {
                MessageBox.Show("Please select at least one item.", "No Selection",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Create result
            Result = new SelectFromListResult
            {
                SelectedItems = selectedItems,
                SelectedSheetSet = ctx_groups_selector_cb.SelectedItem?.ToString(),
                DialogResult = true
            };

            SelectedItems = selectedItems;
            DialogResult = true;
        }

        #endregion

        #region ListView Events

        private void check_selected(object sender, RoutedEventArgs e)
        {
            // CheckBox checked - handled by binding
        }

        private void uncheck_selected(object sender, RoutedEventArgs e)
        {
            // CheckBox unchecked - handled by binding
        }

        private void selected_item_changed(object sender, SelectionChangedEventArgs e)
        {
            // Handle selection change if needed
        }

        #endregion
    }

    #region Supporting Classes

    public class SelectableItem : INotifyPropertyChanged
    {
        private bool _isSelected;

        public object Item { get; set; }
        public string DisplayName { get; set; }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                _isSelected = value;
                OnPropertyChanged(nameof(IsSelected));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class SelectFromListConfig
    {
        public string Title { get; set; } = "Select Items";
        public string ButtonText { get; set; } = "Select";
        public bool ShowSearch { get; set; } = true;
        public bool ShowCheckButtons { get; set; } = true;
        public bool ShowSheetSets { get; set; } = false;
        public bool ShowResetButton { get; set; } = false;
        public List<string> SheetSetOptions { get; set; } = new List<string>();
        public string DefaultSheetSet { get; set; }
        public bool DefaultSelectAll { get; set; } = false;
        public bool RequireSelection { get; set; } = true;

        // Add this new property for sheet filtering
        public List<ViewSheetSet> ViewSheetSets { get; set; } = new List<ViewSheetSet>();
    }

    public class SelectFromListResult
    {
        public List<object> SelectedItems { get; set; } = new List<object>();
        public string SelectedSheetSet { get; set; }
        public bool DialogResult { get; set; }
    }

    #endregion
}