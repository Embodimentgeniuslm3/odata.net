﻿//---------------------------------------------------------------------
// <copyright file="AsynchronousQueryTests.cs" company="Microsoft">
//      Copyright (C) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.
// </copyright>
//---------------------------------------------------------------------

namespace Microsoft.Test.OData.Tests.Client.AsynchronousTests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Reflection;
    using Microsoft.OData.Client;
    using Microsoft.Test.OData.Services.TestServices;
    using Microsoft.Test.OData.Services.TestServices.AstoriaDefaultServiceReference;
    using Xunit.Abstractions;
    using Xunit;

    /// <summary>
    /// Client query tests using asynchronous APIs
    /// - AddQueryOption
    /// </summary>
    public class AsynchronousQueryTests : EndToEndTestBase
    {
        public AsynchronousQueryTests(ITestOutputHelper helper)
            : base(ServiceDescriptors.AstoriaDefaultService, helper)
        {
        }

        /// <summary>
        /// Add a custom query option
        /// </summary>
        [Fact, Asynchronous]
        //Add timeout attribute to avoid Silverlight test stopping responding here.
        //The timeout attribute makes test failed when run in VS. 
        //So apply the timeout attribute only for silverlight case.
        //ToDo: figure out why this case is failed in silverlight test run later.
        public void AddQueryOption_Custom()
        {
            var context = this.CreateWrappedContext<DefaultContainer>().Context;
            var query = context.Customer.AddQueryOption("custom", "true");
            query.BeginExecute(
                (ar) =>
                {
                    var customers = query.EndExecute(ar).ToList();
                    Assert.Equal(2, customers.Count());
                    this.EnqueueTestComplete();
                },
                null);

            this.WaitForTestToComplete();
        }

        /// <summary>
        /// Add $filter query option
        /// </summary>
        [Fact, Asynchronous]
        public void AddQueryOption_Filter()
        {
            var context = this.CreateWrappedContext<DefaultContainer>().Context;
            var query = context.Customer.AddQueryOption("$filter", "true");
            query.BeginExecute(
                (ar) =>
                {
                    var customers = query.EndExecute(ar);
                    Assert.Equal(2, customers.Count());
                    this.EnqueueTestComplete();
                },
                null);

            this.WaitForTestToComplete();
        }

        /// <summary>
        /// Add $select query option
        /// </summary>
        [Fact, Asynchronous]
        public void AddQueryOption_Select()
        {
            var context = this.CreateWrappedContext<DefaultContainer>().Context;
            var query = context.Customer.AddQueryOption("$select", "CustomerId");
            query.BeginExecute(
                (ar) =>
                {
                    var customers = query.EndExecute(ar);
                    Assert.Equal(2, customers.Count());
                    this.EnqueueTestComplete();
                },
                null);

            this.WaitForTestToComplete();
        }

        /// <summary>
        /// Add $orderby query option
        /// </summary>
        [Fact, Asynchronous]
        public void AddQueryOption_OrderBy()
        {
            var context = this.CreateWrappedContext<DefaultContainer>().Context;
            var query = context.Customer.AddQueryOption("$orderby", "CustomerId desc");
            query.BeginExecute(
                (ar) =>
                {
                    var customers = query.EndExecute(ar);
                    Assert.Equal(2, customers.Count());
                    this.EnqueueTestComplete();
                },
                null);

            this.WaitForTestToComplete();
        }

        /// <summary>
        /// Add two query options
        /// </summary>
        [Fact, Asynchronous]
        public void AddQueryOption_TwoQueryOptions()
        {
            var context = this.CreateWrappedContext<DefaultContainer>().Context;
            var query = context.Customer
                .AddQueryOption("$filter", "true")
                .AddQueryOption("$orderby", "CustomerId desc");

            query.BeginExecute(
                (ar) =>
                {
                    var customers = query.EndExecute(ar);
                    Assert.Equal(2, customers.Count());
                    this.EnqueueTestComplete();
                },
                null);

            this.WaitForTestToComplete();
        }

        /// <summary>
        /// Query Entity Set  With Server Driven Paging
        /// </summary>
        [Fact, Asynchronous]
        public void QueryEntitySetWithServerDrivenPagingTest()
        {
            var context = this.CreateWrappedContext<DefaultContainer>().Context;
            var query = context.Customer.IncludeCount();
            var ar = query.BeginExecute(null, null).EnqueueWait(this);
            var response = query.EndExecute(ar) as QueryOperationResponse<Customer>;
            var totalCount = response.Count;
            var count = response.Count();
            var continuation = response.GetContinuation();

            while (continuation != null)
            {
                var ar2 = context.BeginExecute(continuation, null, null).EnqueueWait(this);
                var response2 = context.EndExecute<Customer>(ar2);

                var currentPageCount = (response2 as QueryOperationResponse<Customer>).Count();
                count += currentPageCount;
                continuation = (response2 as QueryOperationResponse<Customer>).GetContinuation();
            }

            Assert.Equal(totalCount, count);
            this.EnqueueTestComplete();
        }

        [Fact, Asynchronous]
        public void PreferCustomInstanceAnotationTest()
        {
            var value = "";
            var context = this.CreateWrappedContext<DefaultContainer>().Context;
            context.SendingRequest2 += (sender, eventArgs) => ((HttpWebRequestMessage)eventArgs.RequestMessage).SetHeader("Prefer", "odata.include-annotations=*");
            context.Configurations.ResponsePipeline.OnEntryEnded((ReadingEntryArgs) => value = (ReadingEntryArgs.Entry.InstanceAnnotations).FirstOrDefault().Name);

            var query = context.Computer.OrderBy(c => c.ComputerId) as DataServiceQuery<Computer>;
            var ar1 = query.BeginExecute(null, null).EnqueueWait(this);
            var response = (query.EndExecute(ar1) as QueryOperationResponse<Computer>);
            foreach (var comp in response)
            {
                Assert.Equal("MyNamespace.CustomAnnotation1", value);
                value = "";
            }

            this.EnqueueTestComplete();
        }

        /// <summary>
        /// ExcuteBatch Requests
        /// </summary>
        [Fact, Asynchronous]
        public void ExecuteBatchTest()
        {
            var context = this.CreateWrappedContext<DefaultContainer>().Context;
            var countOfBatchParts = 0;
            var countOfTimesSenderCalled = 0;
            context.SendingRequest2 += ((sender, args) =>
            {
                if (args.IsBatchPart)
                {
                    countOfBatchParts++;
                }

                countOfTimesSenderCalled++;
            });

            var arBatch = context.BeginExecuteBatch(
                null,
                null,
                new DataServiceRequest[]
                {
                    new DataServiceRequest<Customer>(((from c in context.Customer where c.CustomerId == -8 select c) as DataServiceQuery<Customer>).RequestUri),
                    new DataServiceRequest<Customer>(((from c in context.Customer where c.CustomerId == -6 select c) as DataServiceQuery<Customer>).RequestUri),
                    new DataServiceRequest<Driver>(((from c in context.Driver where c.Name == "1" select c) as DataServiceQuery<Driver>).RequestUri),
                    new DataServiceRequest<Driver>(((from c in context.Driver where c.Name == "3" select c) as DataServiceQuery<Driver>).RequestUri)
                }).EnqueueWait(this);
            DataServiceResponse qr = context.EndExecuteBatch(arBatch);
            string actualValues = "";
            foreach (var r in qr)
            {
                if (r is QueryOperationResponse<Customer>)
                {
                    var customer = (r as QueryOperationResponse<Customer>).Single();
                    actualValues += customer.CustomerId;
                }

                if (r is QueryOperationResponse<Driver>)
                {
                    var driver = (r as QueryOperationResponse<Driver>).Single();
                    actualValues += driver.Name;
                }
            }

            //actualValues == -8-613
            Assert.Equal(actualValues, ("-8-613"));
            Assert.True(countOfBatchParts > 0 && (countOfTimesSenderCalled - countOfBatchParts) == 1, "countOfBatchParts > 0 && (countOfTimesSenderCalled - countOfBatchParts ) == 1");
            this.EnqueueTestComplete();
        }

        /// <summary>
        /// ExcuteBatch Requests
        /// </summary>
        [Fact, Asynchronous]
        public void ExecuteBatchWithSaveChangesOptionsReturnsCorrectResults()
        {
            var context = this.CreateWrappedContext<DefaultContainer>().Context;
            var countOfBatchParts = 0;
            var countOfTimesSenderCalled = 0;
            context.SendingRequest2 += ((sender, args) =>
            {
                if (args.IsBatchPart)
                {
                    countOfBatchParts++;
                }

                countOfTimesSenderCalled++;
            });

            var arBatch = context.BeginExecuteBatch(
                null, // callback
                null, // state
                SaveChangesOptions.BatchWithIndependentOperations | SaveChangesOptions.UseRelativeUri,
                new DataServiceRequest[]
                {
                    new DataServiceRequest<Customer>(((context.Customer.Where(c => c.CustomerId == -8)) as DataServiceQuery<Customer>).RequestUri),
                    new DataServiceRequest<Customer>(((context.Customer.Where(c => c.CustomerId == -6)) as DataServiceQuery<Customer>).RequestUri),
                    new DataServiceRequest<Driver>(((context.Driver.Where(c => c.Name == "1")) as DataServiceQuery<Driver>).RequestUri),
                    new DataServiceRequest<Driver>(((context.Driver.Where(c => c.Name == "3")) as DataServiceQuery<Driver>).RequestUri)
                }).EnqueueWait(this);
            DataServiceResponse qr = context.EndExecuteBatch(arBatch);
            string actualValues = "";
            foreach (var r in qr)
            {
                if (r is QueryOperationResponse<Customer>)
                {
                    var customer = (r as QueryOperationResponse<Customer>).Single();
                    actualValues += customer.CustomerId;
                }

                if (r is QueryOperationResponse<Driver>)
                {
                    var driver = (r as QueryOperationResponse<Driver>).Single();
                    actualValues += driver.Name;
                }
            }

            //actualValues == -8-613
            Assert.Equal(actualValues, ("-8-613"));
            Assert.True(countOfBatchParts > 0 && (countOfTimesSenderCalled - countOfBatchParts) == 1, "countOfBatchParts > 0 && (countOfTimesSenderCalled - countOfBatchParts ) == 1");
            this.EnqueueTestComplete();
        }

        /// <summary>
        /// CancelRequest for Batch Requests
        /// </summary>
        [Fact, Asynchronous]
        public void CancelBatchRequestTest()
        {
            DefaultContainer context = this.CreateWrappedContext<DefaultContainer>().Context;
            ServicePoint servicePoint = null;
            context.SendingRequest2 += ((sender, args) =>
            {
                servicePoint = ServicePointManager.FindServicePoint(args.RequestMessage.Url);
                MethodInfo releaseConns = servicePoint.GetType().GetMethod
                        ("ReleaseAllConnectionGroups",
                        BindingFlags.Instance | BindingFlags.NonPublic);
                releaseConns.Invoke(servicePoint, null);
                if (args.RequestMessage is HttpClientRequestMessage)
                {
                    servicePoint = ServicePointManager.FindServicePoint(args.RequestMessage.Url);
                }
            });

            IAsyncResult arBatch = context.BeginExecuteBatch(
                null,
                null,
                new DataServiceRequest[]
                {
                    new DataServiceRequest<Customer>(((from c in context.Customer where c.CustomerId == -8 select c) as DataServiceQuery<Customer>).RequestUri),
                    new DataServiceRequest<Customer>(((from c in context.Customer where c.CustomerId == -6 select c) as DataServiceQuery<Customer>).RequestUri),
                });

            Assert.Equal(1, servicePoint.CurrentConnections);
            context.CancelRequest(arBatch);
            Assert.Equal(0, servicePoint.CurrentConnections);
        }

        /// <summary>
        /// Query Entity Set
        /// </summary>
        [Fact, Asynchronous]
        public void QueryEntitySetTest()
        {
            var context = this.CreateWrappedContext<DefaultContainer>().Context;
            var query = context.Customer;
            query.BeginExecute(
                ar =>
                {
                    var customers = query.EndExecute(ar).ToList();
                    Assert.Equal(2, customers.Count());
                    this.EnqueueTestComplete();
                },
                null);

            this.WaitForTestToComplete();
        }

        /// <summary>
        /// IncludeCount Test
        /// </summary>
        [Fact, Asynchronous]
        public void IncludeCountTest()
        {
            var context = this.CreateWrappedContext<DefaultContainer>().Context;
            var query = context.Computer.IncludeCount();
            query.BeginExecute(
                (ar) =>
                {
                    var customers = query.EndExecute(ar) as QueryOperationResponse<Computer>;
                    Assert.Equal(10, customers.Count);
                    this.EnqueueTestComplete();
                },
                null);

            this.WaitForTestToComplete();
        }

        /// <summary>
        /// IncludeCount Test
        /// </summary>
        [Fact, Asynchronous]
        public void IncludeCountTestWithServerDrivenPaging()
        {
            var context = this.CreateWrappedContext<DefaultContainer>().Context;
            var query = context.Customer.IncludeCount();
            query.BeginExecute(
                (ar) =>
                {
                    var customers = query.EndExecute(ar) as QueryOperationResponse<Customer>;
                    Assert.Equal(10, customers.Count);
                    this.EnqueueTestComplete();
                },
                null);

            this.WaitForTestToComplete();
        }

        /// <summary>
        /// LINQ query with nested calls to All
        /// </summary>
        [Fact, Asynchronous]
        public void Linq_AllNestedTest()
        {
            var context = this.CreateWrappedContext<DefaultContainer>().Context;
            var query = context.Customer.Where(c => c.Logins.All(l => l.Orders.All(o => o.OrderId > 0))) as DataServiceQuery<Customer>;
            query = query.IncludeCount();
            query.BeginExecute(
                (ar) =>
                {
                    var customers = query.EndExecute(ar) as QueryOperationResponse<Customer>;
                    Assert.Equal(6, customers.Count);
                    this.EnqueueTestComplete();
                },
                null);

            this.WaitForTestToComplete();
        }

        /// <summary>
        /// LINQ query using All()
        /// </summary>
        [Fact, Asynchronous]
        public void Linq_AllTest()
        {
            var context = this.CreateWrappedContext<DefaultContainer>().Context;
            var query = context.Customer.Where(c => c.Orders.All(o => o.OrderId > 0)) as DataServiceQuery<Customer>;
            query = query.IncludeCount();
            query.BeginExecute(
                (ar) =>
                {
                    var customers = query.EndExecute(ar) as QueryOperationResponse<Customer>;
                    Assert.Equal(6, customers.Count);
                    this.EnqueueTestComplete();
                },
                    null);

            this.WaitForTestToComplete();
        }

        /// <summary>
        /// LINQ query using nested calls to Any()
        /// </summary>
        [Fact, Asynchronous]
        public void Linq_AnyNestedTest()
        {
            var context = this.CreateWrappedContext<DefaultContainer>().Context;
            var query = context.Customer.Where(c => c.Logins.Any(l => l.Orders.Any())) as DataServiceQuery<Customer>;
            query = query.IncludeCount();
            query.BeginExecute(
                (ar) =>
                {
                    var customers = query.EndExecute(ar) as QueryOperationResponse<Customer>;
                    Assert.Equal(4, customers.Count);
                    this.EnqueueTestComplete();
                },
                    null);

            this.WaitForTestToComplete();
        }

        /// <summary>
        /// LINQ query using Any()
        /// </summary>
        [Fact, Asynchronous]
        public void Linq_AnyTest()
        {
            var context = this.CreateWrappedContext<DefaultContainer>().Context;
            var query = context.Customer.Where(c => c.Orders.Any(o => o.OrderId < 0)) as DataServiceQuery<Customer>;
            query = query.IncludeCount();

            query.BeginExecute(
                (ar) =>
                {
                    var customers = query.EndExecute(ar) as QueryOperationResponse<Customer>;
                    Assert.Equal(4, customers.Count);
                    this.EnqueueTestComplete();
                },
                null);

            this.WaitForTestToComplete();
        }

        /// <summary>
        /// LINQ query using Expand()
        /// </summary>
        [Fact, Asynchronous]
        public void Linq_ExpandTest()
        {
            var context = this.CreateWrappedContext<DefaultContainer>().Context;
            var query = context.Customer.Expand("Wife").Where(c => c.CustomerId == -10) as DataServiceQuery<Customer>;
            query.BeginExecute(
                (ar) =>
                {
                    var customerWithWife = query.EndExecute(ar).Single();
                    Assert.NotNull(customerWithWife.Wife);
                    this.EnqueueTestComplete();
                },
            null);

            this.WaitForTestToComplete();
        }

        /// <summary>
        /// LINQ query using OrderByDescending()
        /// </summary>
        [Fact, Asynchronous]
        public void Linq_OrderByDescendingTest()
        {
            var context = this.CreateWrappedContext<DefaultContainer>().Context;
            var query = context.Customer.OrderByDescending(c => c.Name) as DataServiceQuery<Customer>;
            query.BeginExecute(
                (ar) =>
                {
                    var customers = query.EndExecute(ar).ToList();
                    Assert.Equal("versioningtaskspurgesizesminusdatarfcactivator", customers.First().Name);
                    Assert.Equal("remotingdestructorprinterswitcheschannelssatellitelanguageresolve", customers.Last().Name);
                    this.EnqueueTestComplete();
                },
                null);

            this.WaitForTestToComplete();
        }

        /// <summary>
        /// LINQ query using OrderByDescending() and ThenByDescending()
        /// </summary>
        [Fact, Asynchronous]
        public void Linq_OrderByDescendingThenByDescendingTest()
        {
            var context = this.CreateWrappedContext<DefaultContainer>().Context;
            var query = context.Customer
                .OrderByDescending(c => c.PrimaryContactInfo.HomePhone.Extension)
                .ThenByDescending(c => c.Name) as DataServiceQuery<Customer>;
            List<Customer> customers = null;
            query.BeginExecute(
                (ar) =>
            {
                try
                {
                    customers = query.EndExecute(ar).ToList();
                }
                finally
                {
                    this.TestCompleted = true;
                }
            },
                null);
            this.EnqueueConditional(() => this.TestCompleted);
            this.EnqueueCallback(() => Assert.NotNull(customers));
            this.EnqueueCallback(() => Assert.Equal("namedpersonalabsentnegationbelowstructuraldeformattercreatebackupterrestrial", customers.First().Name));
            this.EnqueueCallback(() => Assert.Equal("freezeunauthenticatedparentkey", customers.Last().Name));
            this.EnqueueTestComplete();
        }

        /// <summary>
        /// LINQ query using OrderByDescending() and ThenBy()
        /// </summary>
        [Fact, Asynchronous]
        public void Linq_OrderByDescendingThenByTest()
        {
            var context = this.CreateWrappedContext<DefaultContainer>().Context;
            var query = context.Customer
                .OrderByDescending(c => c.PrimaryContactInfo.WorkPhone.PhoneNumber)
                .ThenBy(c => c.Name) as DataServiceQuery<Customer>;

            query.BeginExecute(
                (ar) =>
                {
                    var customers = query.EndExecute(ar).ToList();
                    Assert.Equal("forbuiltinencodedchnlsufficientexternal", customers.First().Name);
                    Assert.Equal("allocatedentitiescontentcontainercurrentsynchronously", customers.Last().Name);
                    this.EnqueueTestComplete();
                },
                null);

            this.WaitForTestToComplete();
        }

        /// <summary>
        /// LINQ query using OrderBy()
        /// </summary>
        [Fact, Asynchronous]
        public void Linq_OrderByTest()
        {
            var context = this.CreateWrappedContext<DefaultContainer>().Context;
            var query = context.Customer.OrderBy(c => c.Name) as DataServiceQuery<Customer>;

            query.BeginExecute(
                (ar) =>
                {
                    var customers = query.EndExecute(ar).ToList();
                    Assert.Equal(null, customers.First().Name);
                    Assert.Equal("allocatedentitiescontentcontainercurrentsynchronously", customers.Last().Name);
                    this.EnqueueTestComplete();
                },
                null);

            this.WaitForTestToComplete();
        }

        /// <summary>
        /// LINQ query using OrderBy() and ThenByDescending()
        /// </summary>
        [Fact, Asynchronous]
        public void Linq_OrderByThenByDescendingTest()
        {
            var context = this.CreateWrappedContext<DefaultContainer>().Context;
            var query = context.Customer
                .Where(c => c.Name != null && c.PrimaryContactInfo != null && c.PrimaryContactInfo.WorkPhone != null && c.PrimaryContactInfo.WorkPhone.PhoneNumber != null)
                .OrderBy(c => c.PrimaryContactInfo.WorkPhone.PhoneNumber)
                .ThenByDescending(c => c.Name) as DataServiceQuery<Customer>;

            query.BeginExecute(
                (ar) =>
                {
                    var customers = query.EndExecute(ar).ToList();
                    Assert.Equal("enumeratetrademarkexecutionbrfalsenesteddupoverflowspacebarseekietfbeforeobservedstart", customers.First().Name);
                    Assert.Equal("versioningtaskspurgesizesminusdatarfcactivator", customers.Last().Name);
                    this.EnqueueTestComplete();
                },
                null);

            this.WaitForTestToComplete();
        }

        /// <summary>
        /// LINQ query using OrderBy() and ThenBy()
        /// </summary>
        [Fact, Asynchronous]
        public void Linq_OrderByThenByTest()
        {
            var context = this.CreateWrappedContext<DefaultContainer>().Context;
            var query = context.Customer
                .OrderBy(c => c.PrimaryContactInfo.WorkPhone.PhoneNumber)
                .ThenBy(c => c.Name) as DataServiceQuery<Customer>;

            query.BeginExecute(
                (ar) =>
                {
                    var customers = query.EndExecute(ar).ToList();
                    Assert.Equal(null, customers.First().Name);
                    Assert.Equal("enumeratetrademarkexecutionbrfalsenesteddupoverflowspacebarseekietfbeforeobservedstart", customers.Last().Name);
                    this.EnqueueTestComplete();
                },
                null);

            this.WaitForTestToComplete();
        }

        /// <summary>
        /// LINQ - project a primitive property from a feed, materialized into a custom entity
        /// </summary>
        [Fact, Asynchronous]
        public void Linq_ProjectIntoCustomEntity_FromSetTest()
        {
            var context = this.CreateWrappedContext<DefaultContainer>().Context;
            var query = (from c in context.Customer
                         select new EntityCustomer { Name = c.Name }) as DataServiceQuery<EntityCustomer>;

            query.BeginExecute(
                (ar) =>
                {
                    var customers = query.EndExecute(ar);
                    foreach (var customer in customers)
                    {
                        Assert.Equal(0, customer.CustomerId);
                        Assert.NotNull(customer.Name);
                    }
                    this.EnqueueTestComplete();
                },
                null);

            this.WaitForTestToComplete();
        }

        /// <summary>
        /// LINQ - project a primitive property from a feed, materialized into a non entity object using the constructor
        /// </summary>
        [Fact, Asynchronous]
        public void Linq_ProjectIntoNonEntityUsingConstructor_FromSetTest()
        {
            var context = this.CreateWrappedContext<DefaultContainer>().Context;
            var query = (from c in context.Customer
                         select new NonEntityCustomer2(1000, c.Name)) as DataServiceQuery<NonEntityCustomer2>;

            query.BeginExecute(
                (ar) =>
            {
                var customers = query.EndExecute(ar);

                foreach (var customer in customers)
                {
                    Assert.Equal(1000, customer.CustomerId);
                    Assert.NotNull(customer.Name);
                }
                this.EnqueueTestComplete();
            },
            null);

            this.WaitForTestToComplete();
        }

        /// <summary>
        /// LINQ - project a primitive property from a feed, materialized into a non entity object using property initializers
        /// </summary>
        [Fact, Asynchronous]
        public void Linq_ProjectIntoNonEntityUsingInitializers_FromSetTest()
        {
            var context = this.CreateWrappedContext<DefaultContainer>().Context;
            var query = (from c in context.Customer
                         select new NonEntityCustomer { CustomerId = 1000, Name = c.Name }) as DataServiceQuery<NonEntityCustomer>;

            query.BeginExecute(
                (ar) =>
                {
                    var customers = query.EndExecute(ar);

                    foreach (var customer in customers)
                    {
                        Assert.Equal(1000, customer.CustomerId);
                        Assert.NotNull(customer.Name);
                    }
                    this.EnqueueTestComplete();
                },
                null);

            this.WaitForTestToComplete();
        }

        /// <summary>
        /// LINQ - project a primitive property from a feed
        /// </summary>
        [Fact, Asynchronous]
        public void Linq_ProjectOneProperty_FromSetTest()
        {
            var context = this.CreateWrappedContext<DefaultContainer>().Context;
            var query = (from c in context.Customer
                         select new Customer { Name = c.Name }) as DataServiceQuery<Customer>;

            query.BeginExecute(
                (ar) =>
                {
                    var customers = query.EndExecute(ar);
                    foreach (var customer in customers)
                    {
                        Assert.Equal(0, customer.CustomerId);
                        Assert.NotNull(customer.Name);
                    }

                    this.EnqueueTestComplete();
                },
                null);

            this.WaitForTestToComplete();
        }

        /// <summary>
        /// LINQ - project two primitive property from a feed
        /// </summary>
        [Fact, Asynchronous]
        public void Linq_ProjectTwoProperties_FromSetTest()
        {
            var context = this.CreateWrappedContext<DefaultContainer>().Context;
            var query = (from c in context.Customer
                         select new Customer { Name = c.Name, CustomerId = c.CustomerId }) as DataServiceQuery<Customer>;

            query.BeginExecute(
                (ar) =>
                {
                    var customers = query.EndExecute(ar);

                    foreach (var customer in customers)
                    {
                        Assert.NotEqual(0, customer.CustomerId);
                        Assert.NotNull(customer.Name);
                    }
                    this.EnqueueTestComplete();
                },
                null);

            this.WaitForTestToComplete();
        }

        /// <summary>
        /// LINQ query Project properties from entity and expanded entity
        /// </summary>
        [Fact, Asynchronous]
        public void Linq_ProjectPropertiesFromEntityandExpandedEntity()
        {
            var context = this.CreateWrappedContext<DefaultContainer>().Context;
            var query = (from c in context.Computer
                         where c.ComputerId == -10
                         select new Computer { ComputerId = c.ComputerId, ComputerDetail = new ComputerDetail { ComputerDetailId = c.ComputerDetail.ComputerDetailId } }
                         ) as DataServiceQuery<Computer>;
            var ar = query.BeginExecute(null, null).EnqueueWait(this);
            var c1 = query.EndExecute(ar).Single();
            Assert.Equal(-10, c1.ComputerId);
            Assert.Equal(-10, c1.ComputerDetail.ComputerDetailId);

            this.EnqueueTestComplete();
        }

        /// <summary>
        /// LINQ query Project Name Stream Property
        /// </summary>
        [Fact, Asynchronous]
        public void Linq_ProjectNameStreamProperty()
        {
            var context = this.CreateWrappedContext<DefaultContainer>().Context;
            var query = (from c in context.Customer
                         where c.CustomerId == -10
                         select new Customer { CustomerId = c.CustomerId, Video = c.Video }) as DataServiceQuery<Customer>;
            query.BeginExecute(
                   (ar) =>
                   {
                       var c1 = query.EndExecute(ar).SingleOrDefault();
                       Assert.Equal(-10, c1.CustomerId);
                       Assert.NotNull(c1.Video);
                       this.EnqueueTestComplete();
                   },
                   null);

            this.WaitForTestToComplete();
        }

        /// <summary>
        /// LINQ query Order By Canonical Fuctions
        /// </summary>
        [Fact, Asynchronous]
        public void Linq_OrderByCanonicalFunctionString()
        {
            var name = "commastartedtotalnormaloffsetsregisteredgroupcelestialexposureconventionsimportcastclass";
            var context = this.CreateWrappedContext<DefaultContainer>().Context;
            var q0 = (from c in context.Customer
                      orderby c.Name.Contains(name) && c.Name.Length == name.Length
                      select new Customer { CustomerId = c.CustomerId, Name = c.Name, }) as DataServiceQuery<Customer>;
            var ar0 = q0.BeginExecute(null, null).EnqueueWait(this);
            var value0 = q0.EndExecute(ar0).ToList();
            Assert.Equal(-8, value0[1].CustomerId);

            this.EnqueueTestComplete();
        }

        /// <summary>
        /// LINQ query Order By Canonical Fuctions
        /// </summary>
        [Fact, Asynchronous]
        public void Linq_OrderByCanonicalFunctionMath()
        {
            var context = this.CreateWrappedContext<DefaultContainer>().Context;
            var q1 = (from c in context.ComputerDetail
                      orderby System.Math.Ceiling(c.Dimensions.Depth) < 0
                      select c) as DataServiceQuery<ComputerDetail>;
            var ar1 = q1.BeginExecute(null, null).EnqueueWait(this);
            var value1 = q1.EndExecute(ar1).ToList();
            Assert.True(value1.First().Dimensions.Depth > value1.Last().Dimensions.Depth);
        }

        /// <summary>
        /// LINQ query Order By Canonical Fuctions
        /// </summary>
        [Fact, Asynchronous]
        public void Linq_OrderByCanonicalFunctionDateTime()
        {
            var context = this.CreateWrappedContext<DefaultContainer>().Context;
            var q2 = (from c in context.ComputerDetail
                      orderby c.PurchaseDate.Day == 15 && c.PurchaseDate.Year == 2020
                      select c) as DataServiceQuery<ComputerDetail>;
            var ar2 = q2.BeginExecute(null, null).EnqueueWait(this);
            var value2 = q2.EndExecute(ar2).ToList();
            Assert.True(value2.First().ComputerDetailId == -9);
            Assert.True(value2.Last().ComputerDetailId == -10);

            this.EnqueueTestComplete();
        }

        /// <summary>
        /// LINQ query Order By Canonical Fuctions
        /// </summary>
        [Fact, Asynchronous]
        public void Linq_OrderByCanonicalFunctionInt()
        {
            var context = this.CreateWrappedContext<DefaultContainer>().Context;
            var q4 = (from c in context.Person.OfType<Employee>()
                      orderby c.ManagersPersonId
                      select c) as DataServiceQuery<Employee>;
            var ar = q4.BeginExecute(null, null).EnqueueWait(this);
            var value3 = q4.EndExecute(ar).ToList();
            Assert.Equal(5309, value3.Last().ManagersPersonId);

            this.EnqueueTestComplete();
        }

        /// <summary>
        /// LINQ query Filter With Canonical Fuctions
        /// </summary>
        [Fact, Asynchronous]
        public void Linq_FilterWithCanonicalFunctionString()
        {
            var name = "commastartedtotalnormaloffsetsregisteredgroupcelestialexposureconventionsimportcastclass";
            var context = this.CreateWrappedContext<DefaultContainer>().Context;
            var q0 = (from c in context.Customer
                      where c.Name.Contains(name) && c.Name.Length == name.Length
                      select new Customer { CustomerId = c.CustomerId, Name = c.Name, }) as DataServiceQuery<Customer>;
            var ar0 = q0.BeginExecute(null, null).EnqueueWait(this);
            var value0 = q0.EndExecute(ar0).Single();
            Assert.Equal(name, value0.Name);

            this.EnqueueTestComplete();
        }

        /// <summary>
        /// LINQ query Order By Canonical Fuctions
        /// </summary>
        [Fact, Asynchronous]
        public void Linq_FilterCanonicalFunctionMath()
        {
            var context = this.CreateWrappedContext<DefaultContainer>().Context;
            var q1 = (from c in context.ComputerDetail
                      where System.Math.Ceiling(c.Dimensions.Depth) < 0
                      select c) as DataServiceQuery<ComputerDetail>;
            var ar1 = q1.BeginExecute(null, null).EnqueueWait(this);
            var value1 = q1.EndExecute(ar1).ToList();
            Assert.Equal(4, value1.Count);
        }

        /// <summary>
        /// LINQ query Order By Canonical Fuctions
        /// </summary>
        [Fact, Asynchronous]
        public void Linq_FilterCanonicalFunctionDate()
        {
            var context = this.CreateWrappedContext<DefaultContainer>().Context;
            var q2 = (from c in context.ComputerDetail
                      where c.PurchaseDate.Day == 15 && c.PurchaseDate.Year == 2020
                      select c) as DataServiceQuery<ComputerDetail>;
            var ar2 = q2.BeginExecute(null, null).EnqueueWait(this);
            var value2 = q2.EndExecute(ar2).Single();
            Assert.Equal(-10, value2.ComputerDetailId);

            this.EnqueueTestComplete();
        }

        /// <summary>
        /// LINQ query Order By Canonical Fuctions
        /// </summary>
        [Fact, Asynchronous]
        public void Linq_FilterCanonicalFunctionInt()
        {
            var context = this.CreateWrappedContext<DefaultContainer>().Context;
            var q4 = (from c in context.Person.OfType<Employee>()
                      where c.ManagersPersonId == 47
                      select c) as DataServiceQuery<Employee>;
            var ar = q4.BeginExecute(null, null).EnqueueWait(this);
            var value3 = q4.EndExecute(ar).ToList();
            Assert.Equal(1, value3.Count);

            this.EnqueueTestComplete();
        }

        /// <summary>
        /// LINQ - project a primitive property from a single entity into a custom entity
        /// </summary>
        [Fact, Asynchronous]
        public void Linq_ProjectIntoEntity_FromSingleEntityTest()
        {
            var context = this.CreateWrappedContext<DefaultContainer>().Context;
            var query = (from c in context.Customer
                         where c.CustomerId == -10
                         select new EntityCustomer { Name = c.Name }) as DataServiceQuery<EntityCustomer>;

            query.BeginExecute(
                (ar) =>
                {
                    var customer = query.EndExecute(ar).Single();

                    Assert.Equal(0, customer.CustomerId);
                    Assert.Equal("commastartedtotalnormaloffsetsregisteredgroupcelestialexposureconventionsimportcastclass", customer.Name);
                    this.EnqueueTestComplete();
                },
                null);

            this.WaitForTestToComplete();
        }

        /// <summary>
        /// LINQ - project a primitive property from a single entity, materialized into a non entity object using the constructor
        /// </summary>
        [Fact, Asynchronous]
        public void Linq_ProjectIntoNonEntityUsingConstructor_FromSingleEntityTest()
        {
            var context = this.CreateWrappedContext<DefaultContainer>().Context;
            var query = (from c in context.Customer
                         where c.CustomerId == -10
                         select new NonEntityCustomer2(1000, c.Name)) as DataServiceQuery<NonEntityCustomer2>;

            query.BeginExecute(
                (ar) =>
                {
                    var customer = query.EndExecute(ar).Single();

                    Assert.Equal(1000, customer.CustomerId);
                    Assert.Equal("commastartedtotalnormaloffsetsregisteredgroupcelestialexposureconventionsimportcastclass", customer.Name);
                    this.EnqueueTestComplete();
                },
                null);

            this.WaitForTestToComplete();
        }

        /// <summary>
        /// LINQ - project a primitive property from a single entity, materialized into a non entity object using property initializers
        /// </summary>
        [Fact, Asynchronous]
        public void Linq_ProjectIntoNonEntityUsingInitializers_FromSingleEntityTest()
        {
            var context = this.CreateWrappedContext<DefaultContainer>().Context;
            var query = (from c in context.Customer
                         where c.CustomerId == -10
                         select new NonEntityCustomer { CustomerId = 1000, Name = c.Name }) as DataServiceQuery<NonEntityCustomer>;

            query.BeginExecute(
                (ar) =>
                {
                    var customer = query.EndExecute(ar).Single();

                    Assert.Equal(1000, customer.CustomerId);
                    Assert.Equal("commastartedtotalnormaloffsetsregisteredgroupcelestialexposureconventionsimportcastclass", customer.Name);
                    this.EnqueueTestComplete();
                },
                null);

            this.WaitForTestToComplete();
        }

        /// <summary>
        /// LINQ - project an EPM property from a feed
        /// </summary>
        [Fact, Asynchronous]
        public void Linq_ProjectMappedProperties_FromSetTest()
        {
            var context = this.CreateWrappedContext<DefaultContainer>().Context;
            var query = (from c in context.Customer
                         select new Customer { CustomerId = c.CustomerId }) as DataServiceQuery<Customer>;

            query.BeginExecute(
                (ar) =>
                {
                    var customers = query.EndExecute(ar);

                    foreach (var customer in customers)
                    {
                        Assert.NotEqual(0, customer.CustomerId);
                        Assert.Null(customer.Name);
                    }
                    this.EnqueueTestComplete();
                },
                null);

            this.WaitForTestToComplete();
        }

        /// <summary>
        /// LINQ - project an EPM property from a single entity
        /// </summary>
        [Fact, Asynchronous]
        public void Linq_ProjectMappedProperties_FromSingleEntityTest()
        {
            var context = this.CreateWrappedContext<DefaultContainer>().Context;
            var query = (from c in context.Customer
                         where c.CustomerId == -10
                         select new Customer { CustomerId = c.CustomerId }) as DataServiceQuery<Customer>;

            query.BeginExecute(
                (ar) =>
                {
                    var customer = query.EndExecute(ar).Single();

                    Assert.Equal(-10, customer.CustomerId);
                    Assert.Equal(null, customer.Name);
                    this.EnqueueTestComplete();
                },
                null);

            this.WaitForTestToComplete();
        }

        /// <summary>
        /// LINQ - project a navigation property from a feed
        /// </summary>
        [Fact, Asynchronous]
        public void Linq_ProjectNavigationProperty_FromSetTest()
        {
            var context = this.CreateWrappedContext<DefaultContainer>().Context;
            var query = (from c in context.Customer
                         select new Customer { Wife = c.Wife }) as DataServiceQuery<Customer>;

            query.BeginExecute(
                (ar) =>
                {
                    var customers = query.EndExecute(ar);

                    foreach (var customer in customers)
                    {
                        Assert.NotNull(customer.Wife);
                    }
                    this.EnqueueTestComplete();
                },
                null);

            this.WaitForTestToComplete();
        }

        /// <summary>
        /// LINQ - project a navigation property from a single entity
        /// </summary>
        [Fact, Asynchronous]
        public void Linq_ProjectNavigationProperty_FromSingleEntityTest()
        {
            var context = this.CreateWrappedContext<DefaultContainer>().Context;
            var query = (from c in context.Customer
                         where c.CustomerId == -10
                         select new Customer { Wife = c.Wife }) as DataServiceQuery<Customer>;

            query.BeginExecute(
                (ar) =>
                {
                    var customer = query.EndExecute(ar).Single();

                    Assert.NotNull(customer.Wife);
                    this.EnqueueTestComplete();
                },
                null);

            this.WaitForTestToComplete();
        }

        /// <summary>
        /// LINQ - project a primitive property from a single entity
        /// </summary>
        [Fact, Asynchronous]
        public void Linq_ProjectProperty_FromSingleEntityTest()
        {
            var context = this.CreateWrappedContext<DefaultContainer>().Context;
            var query = (from c in context.Customer
                         where c.CustomerId == -10
                         select new Customer { Name = c.Name }) as DataServiceQuery<Customer>;

            query.BeginExecute(
                (ar) =>
                {
                    var customer = query.EndExecute(ar).Single();

                    Assert.Equal(0, customer.CustomerId);
                    Assert.Equal("commastartedtotalnormaloffsetsregisteredgroupcelestialexposureconventionsimportcastclass", customer.Name);
                    this.EnqueueTestComplete();
                },
                null);

            this.WaitForTestToComplete();
        }

        [Fact, Asynchronous]
        public void Linq_ProjectTwoProperties_FromSingleEntityTest()
        {
            var context = this.CreateWrappedContext<DefaultContainer>().Context;
            var query = (from c in context.Customer
                         where c.CustomerId == -10
                         select new Customer { Name = c.Name, CustomerId = c.CustomerId }) as DataServiceQuery<Customer>;

            query.BeginExecute(
                (ar) =>
                {
                    var customer = query.EndExecute(ar).Single();

                    Assert.Equal(-10, customer.CustomerId);
                    Assert.Equal("commastartedtotalnormaloffsetsregisteredgroupcelestialexposureconventionsimportcastclass", customer.Name);
                    this.EnqueueTestComplete();
                },
                null);

            this.WaitForTestToComplete();
        }

        /// <summary>
        /// LINQ query using Skip() an Take()
        /// </summary>
        [Fact, Asynchronous]
        public void Linq_SkipTakeTest()
        {
            var context = this.CreateWrappedContext<DefaultContainer>().Context;
            var query = context.Computer.OrderBy(c => c.ComputerId).Skip(1).Take(3) as DataServiceQuery<Computer>;

            query.BeginExecute(
                (ar) =>
                {
                    var computers = query.EndExecute(ar).ToList();
                    Assert.Equal(-9, computers.First().ComputerId);
                    Assert.Equal(-7, computers.Last().ComputerId);
                    this.EnqueueTestComplete();
                },
                null);

            this.WaitForTestToComplete();
        }

        /// <summary>
        /// LINQ query using Skip()
        /// </summary>
        [Fact, Asynchronous]
        public void Linq_SkipTest()
        {
            var context = this.CreateWrappedContext<DefaultContainer>().Context;
            var query = context.Computer.OrderBy(c => c.ComputerId).Skip(1) as DataServiceQuery<Computer>;

            query.BeginExecute(
                (ar) =>
                {
                    var computers = query.EndExecute(ar).ToList();
                    Assert.Equal(-9, computers.First().ComputerId);
                    Assert.Equal(-1, computers.Last().ComputerId);

                    this.EnqueueTestComplete();
                },
                null);

            this.WaitForTestToComplete();
        }

        /// <summary>
        /// LINQ query using Take()
        /// </summary>
        [Fact, Asynchronous]
        public void Linq_TakeTest()
        {
            var context = this.CreateWrappedContext<DefaultContainer>().Context;
            var query = context.Computer.OrderBy(c => c.ComputerId).Take(2) as DataServiceQuery<Computer>;

            query.BeginExecute(
                (ar) =>
                {
                    var computers = query.EndExecute(ar).ToList();
                    Assert.Equal(-10, computers.First().ComputerId);
                    Assert.Equal(-9, computers.Last().ComputerId);
                    this.EnqueueTestComplete();
                },
                null);

            this.WaitForTestToComplete();
        }

        /// <summary>
        /// LINQ - project two primitive property from a single entity
        /// </summary>
        [Fact, Asynchronous]
        public void Linq_Where_GreaterThan()
        {
            var context = this.CreateWrappedContext<DefaultContainer>().Context;
            var query = (from c in context.Customer where c.CustomerId > 0 select c) as DataServiceQuery<Customer>;

            query.BeginExecute(
                (ar) =>
                {
                    var customers = query.EndExecute(ar);

                    foreach (var customer in customers)
                    {
                        Assert.True(customer.CustomerId > 0);
                    }

                    this.EnqueueTestComplete();
                },
                null);

            this.WaitForTestToComplete();
        }

        /// <summary>
        /// When DataServiceContext.KeyComparisonGeneratesFilterQuery=true, An expression that compares only the key property, will generate a $filter query option.
        /// </summary>
        [Fact]
        public void Linq_Where_Generates_Filter_When_KeyComparisonGeneratesFilterQuery_Is_True()
        {
            var context = this.CreateWrappedContext<DefaultContainer>().Context;
            context.KeyComparisonGeneratesFilterQuery = true;
            var query = context.Customer.Where(c => c.CustomerId == -10);
            var uri = query.ToString();
            Assert.EndsWith("$filter=CustomerId eq -10", uri);
        }

        /// <summary>
        /// When DataServiceContext.KeyComparisonGeneratesFilterQuery=false, An expression that compares only the key property, should create a ByKey Uri.
        /// </summary>
        [Fact]
        public void Linq_Where_Generates_ByKey_When_KeyComparisonGeneratesFilterQuery_Is_False()
        {
            var context = this.CreateWrappedContext<DefaultContainer>().Context;
            // By default context.KeyComparisonGeneratesFilterQuery = false;
            // So we don't need to set it
            var query = context.Customer.Where(c => c.CustomerId == -10);
            var uri = query.ToString();
            Assert.EndsWith("Customer(-10)", uri);
        }

        /// <summary>
        /// Custom Data Service Entity
        /// </summary>
        [KeyAttribute("CustomerId")]
        public class EntityCustomer
        {
            public int CustomerId { get; set; }
            public string Name { get; set; }
        }

        /// <summary>
        /// A Non Entity Class
        /// </summary>
        public class NonEntityCustomer
        {
            public int CustomerId { get; set; }
            public string Name { get; set; }
        }

        /// <summary>
        /// A Non Entity Class
        /// </summary>
        public class NonEntityCustomer2
        {
            public NonEntityCustomer2(int customerId, string name)
            {
                this.CustomerId = customerId;
                this.Name = name;
            }

            public int CustomerId { get; private set; }
            public string Name { get; private set; }
        }
    }
}
