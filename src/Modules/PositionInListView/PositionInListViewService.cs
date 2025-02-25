﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Runtime.CompilerServices;
using DevExpress.Data;
using DevExpress.Data.Filtering;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Editors;
using DevExpress.ExpressApp.Model;
using DevExpress.Xpo;
using Xpand.Extensions.LinqExtensions;
using Xpand.Extensions.Reactive.Transform;
using Xpand.Extensions.Reactive.Utility;
using Xpand.Extensions.XAF.ModelExtensions;
using Xpand.Extensions.XAF.ViewExtensions;
using Xpand.Extensions.XAF.XafApplicationExtensions;
using Xpand.XAF.Modules.Reactive.Services;

namespace Xpand.XAF.Modules.PositionInListView{
    public static class PositionInListViewService{
        internal static IObservable<Unit> Connect(this ApplicationModulesManager manager) 
            => manager.WhenApplication(application => {
                var positionInListCreated = application.WhenPositionInListCreated();
                return positionInListCreated.SortCollectionSource()
                    .Merge(positionInListCreated.DisableSorting())
                    .Merge(application.ReturnObservable().PositionNewObjects());
            });

        internal static IObservable<TSource> TracePositionInListView<TSource>(this IObservable<TSource> source, Func<TSource,string> messageFactory=null,string name = null, Action<string> traceAction = null,
	        Func<Exception,string> errorMessageFactory=null, ObservableTraceStrategy traceStrategy = ObservableTraceStrategy.All,
	        [CallerMemberName] string memberName = "",[CallerFilePath] string sourceFilePath = "",[CallerLineNumber] int sourceLineNumber = 0) 
            => source.Trace(name, PositionInListViewModule.TraceSource,messageFactory,errorMessageFactory, traceAction, traceStrategy, memberName);


        private static IObservable<ListView> WhenPositionInListCreated(this XafApplication application) 
            => application.WhenListViewCreated().Where(_ => application.Model.IsPositionInListView(_.Model.Id));

        private static IObservable<Unit> DisableSorting(this IObservable<ListView> source) 
            => source
                .SelectMany(listView => {
                    var platform = listView.Application().GetPlatform();
                    var modelColumn = listView.PositionMember().modelColumn;
                    return listView.WhenControlsCreated()
                        .SelectMany(view => ((ColumnsListEditor) view.Editor).Columns)
                        .Where(wrapper => platform != Platform.Blazor || wrapper.Id!=modelColumn.PropertyName);
                })
                .Do(wrapper => {
                    wrapper.AllowGroupingChange = false;
                    wrapper.AllowSortingChange = false;
                })
                .TracePositionInListView(wrapper => wrapper.Caption)
                .ToUnit();

        private static IObservable<Unit> SortCollectionSource(this IObservable<ListView> source) 
            => source
                .Select(listView => {
                    var t = listView.PositionMember();
                    foreach (var listViewColumn in t.modelColumn.GetParent<IModelListView>().Columns){
                        listViewColumn.SortOrder=ColumnSortOrder.None;
                        listViewColumn.SortIndex = -1;
                    }

                    if (listView.Application().GetPlatform() == Platform.Blazor) {
                        t.modelColumn.Index = -1;
                    }
                    t.modelColumn.SortIndex = 0;
                    t.modelColumn.SortOrder = (ColumnSortOrder) Enum.Parse(typeof(ColumnSortOrder), t.item.SortingDirection.ToString());
                    listView.CollectionSource.Sorting=new List<SortProperty>(){new(t.item.PositionMember.MemberInfo.Name, t.item.SortingDirection)};
                    return listView;
                })
                .TracePositionInListView(view => view.Id)
                .ToUnit();

        private static (IModelColumn modelColumn, IModelPositionInListViewListViewItem item) PositionMember(this ListView listView) {
            var item = listView.Application().Model.ModelPositionInListView()
                .ListViewItems.First(rule => rule.ListView.Id() == listView.Id);
            var modelColumn = listView.Model.Columns.FirstOrDefault(column => column.ModelMember==item.PositionMember);
            if (modelColumn == null){
                modelColumn = listView.Model.Columns.AddNode<IModelColumn>();
                modelColumn.PropertyName = item.PositionMember.Name;
                modelColumn.Index = -1;
            }
            return (modelColumn,item);
        }

        internal static bool IsPositionInListView(this IModelApplication applicationModel, string viewID) 
            => applicationModel.ModelPositionInListView().ListViewItems.Select(item => item.ListView.Id()).Contains(viewID);

        private static IObservable<Unit> PositionNewObjects(this IObservable<XafApplication> whenApplication) 
            => whenApplication.SelectMany(application => Observable.Defer(() => application.WhenObjectSpaceCreated()
                            .SelectMany(t => t.e.ObjectSpace.WhenNewObjectCommiting<object>().Pair(t.e.ObjectSpace)))
                        .Do(t => {
                            var modelPositionInListView = application.Model.ModelPositionInListView();
                            var listViewItems = modelPositionInListView.ListViewItems;
                            if (listViewItems.Any()) {
                                var objectType = t.source.GetType();
                                var listViewItem = listViewItems.FirstOrDefault(item =>
                                    item.ListView.ModelClass.TypeInfo.Type == objectType);
                                if (listViewItem != null) {
                                    var modelClassItem = modelPositionInListView.ModelClassItems
                                        .FirstOrDefault(item =>
                                            item.ModelClass.TypeInfo.Type == objectType &&
                                            item.ModelMember == listViewItem.PositionMember);
                                    var aggregate = Aggregate.Max;
                                    if (modelClassItem != null && modelClassItem.NewObjectsStrategy ==
                                        PositionInListViewNewObjectsStrategy.First) {
                                        aggregate = Aggregate.Min;
                                    }

                                    var memberInfo = listViewItem.PositionMember.MemberInfo;
                                    var aggregateOperand = new AggregateOperand("", memberInfo.Name, aggregate);
                                    var value = (int) (t.other.Evaluate(objectType, aggregateOperand, null) ?? 0);
                                    var allValues = t.other.ModifiedObjects.Cast<object>()
                                        .Select(o => memberInfo.GetValue(o) ?? 0).Cast<int>().Concat(value.YieldItem());
                                    if (aggregate == Aggregate.Max) {
                                        value = allValues.Max();
                                        value++;
                                    }
                                    else {
                                        value = allValues.Min();
                                        value--;
                                    }

                                    memberInfo.SetValue(t.source, value);
                                }
                            }
                        }))
                
                .TracePositionInListView(_ => $"{_.source}")
                .ToUnit();

        
    }
}