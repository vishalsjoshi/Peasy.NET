﻿using Facile.Core.Extensions;
using Orders.com.BLL;
using Orders.com.Core.Domain;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Orders.com.WPF.VM
{
    public class CustomerOrderVM : OrdersDotComVMBase<Order>
    {
        private CustomerService _customerService;
        private OrderItemService _orderItemService;
        private OrderService _orderService;
        private IEnumerable<Customer> _customers;
        private ObservableCollection<OrderItemVM> _orderItems;
        private ICommand _saveOrderCommand;
        private ICommand _addOrderItemCommand;
        private ICommand _deleteSelectedItemCommand;
        private ProductService _productService;
        private CategoryService _categoryService;
        private EventAggregator _eventAggregator;

        public CustomerOrderVM(EventAggregator eventAggregator, OrderService orderService, CustomerService customerService, OrderItemService orderItemService, CategoryService categoryService, ProductService productService)
            : base(orderService)
        {
            Setup(eventAggregator, orderService, customerService, orderItemService, categoryService, productService);
        }

        public CustomerOrderVM(EventAggregator eventAggregator, Order order, OrderService orderService, CustomerService customerService, OrderItemService orderItemService, CategoryService categoryService, ProductService productService)
            : base(order, orderService)
        {
            Setup(eventAggregator, orderService, customerService, orderItemService, categoryService, productService);
            LoadOrderItems();
        }


        private void Setup(EventAggregator eventAggregator, OrderService orderService, CustomerService customerService, OrderItemService orderItemService, CategoryService categoryService, ProductService productService)
        {
            _orderService = orderService;
            _customerService = customerService;
            _orderItemService = orderItemService;
            _categoryService = categoryService;
            _productService = productService;
            _eventAggregator = eventAggregator;
            _orderItems = new ObservableCollection<OrderItemVM>();
            _saveOrderCommand = new Command(() => SaveAsync(), CanSave);
            _addOrderItemCommand = new Command(() => AddOrderItem(), CanSave);
            _deleteSelectedItemCommand = new Command(() => DeleteSelectedItem());
        }

        public IEnumerable<Customer> Customers
        {
            get
            {
                if (_customers == null)
                    LoadCustomers();

                return _customers;
            }
            private set
            {
                _customers = value;
                OnPropertyChanged("Customers");
            }
        }

        public OrderItemVM SelectedOrderItem
        {
            get;
            set;
        }

        public long ID
        {
            get { return CurrentEntity.OrderID; }
        }

        public decimal Total
        {
            get
            {
                if (!OrderItems.Any()) return 0;
                return OrderItems.Sum(o => o.Amount.GetValueOrDefault());
            }
        }

        public long CurrentCustomerID
        {
            get { return CurrentEntity.CustomerID; }
            set
            {
                CurrentEntity.CustomerID = value;
                IsDirty = true;
                OnPropertyChanged("CurrentCustomerID");
            }
        }

        public IEnumerable<OrderItemVM> OrderItems
        {
            get { return _orderItems; }
        }

        public ICommand AddOrderItemCommand
        {
            get { return _addOrderItemCommand; }
        }

        public ICommand SaveOrderCommand
        {
            get { return _saveOrderCommand; }
        }

        public ICommand DeleteSelectedItemCommand
        {
            get { return _deleteSelectedItemCommand; }
        }

        private async void LoadOrderItems()
        {
            var result = await _orderItemService.GetByOrderCommand(CurrentEntity.OrderID).ExecuteAsync();
            result.Value.ForEach(i => LoadOrderItem(i));
        }

        private async void LoadCustomers()
        {
            var result = await _customerService.GetAllCommand().ExecuteAsync();
            Customers = result.Value;
            OnPropertyChanged("CurrentCustomerID");
        }

        protected override void OnInsertSuccess(Facile.Core.ExecutionResult<Order> result)
        {
            OnPropertyChanged("ID");
            _eventAggregator.SendMessage<OrderInsertedEvent>(new OrderInsertedEvent() { Order = this });
        }

        protected override void OnUpdateSuccess(Facile.Core.ExecutionResult<Order> result)
        {
            _eventAggregator.SendMessage<CustomerOrderUpdatedEvent>(new CustomerOrderUpdatedEvent(this));
        }

        public override async Task SaveAsync()
        {
            await base.SaveAsync();
            var results = OrderItems.ForEach(item => item.OrderID = CurrentEntity.OrderID)
                                    .Select(vm => vm.SaveAsync())
                                    .ToArray();
            await Task.WhenAll(results);
        }

        private void AddOrderItem()
        {
            var item = new OrderItemVM(_orderItemService, _categoryService, _productService);
            SubscribeHandlers(item);
            _orderItems.Add(item);
        }

        private void LoadOrderItem(OrderItem orderItem)
        {
            var item = new OrderItemVM(orderItem, _categoryService, _orderItemService, _productService);
            SubscribeHandlers(item);
            _orderItems.Add(item);
        }

        private void SubscribeHandlers(OrderItemVM item)
        {
            item.EntitySaved += (s, e) => _eventAggregator.SendMessage<CustomerOrderUpdatedEvent>(new CustomerOrderUpdatedEvent(this));
            item.EntityDeleted += (s, e) =>
            {
                _orderItems.Remove(SelectedOrderItem);
                OnPropertyChanged("Total");
                _eventAggregator.SendMessage<CustomerOrderUpdatedEvent>(new CustomerOrderUpdatedEvent(this));
                SelectedOrderItem = null;
            };
            item.PropertyChanged += (s, e) => OnPropertyChanged("Total");
        }

        private async Task DeleteSelectedItem()
        {
            await SelectedOrderItem.DeleteAsync();
        }
    }
}
