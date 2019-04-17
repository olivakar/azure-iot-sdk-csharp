// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.Client.Test
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Net.Http;
    using System.Net.Sockets;
    using System.Net.WebSockets;
    using System.Security.Authentication;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Codecs;
    using Microsoft.Azure.Amqp;
    using Microsoft.Azure.Devices.Client.Common;
    using Microsoft.Azure.Devices.Client.Exceptions;
    using Microsoft.Azure.Devices.Client.Transport;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using NSubstitute;

    [TestClass]
    [TestCategory("Unit")]
    public class ErrorDelegatingHandlerTests
    {
        private const string ErrorMessage = "Error occurred.";
        
        public static IEnumerable<object[]> TestMatrix
        {
            get
            {
                // NonTransient Exception
                yield return CreateTuple(typeof(MessageTooLargeException), typeof(MessageTooLargeException));
                yield return CreateTuple(typeof(DeviceMessageLockLostException), typeof(DeviceMessageLockLostException));
                yield return CreateTuple(typeof(UnauthorizedException), typeof(UnauthorizedException));
                yield return CreateTuple(typeof(IotHubNotFoundException), typeof(IotHubNotFoundException));
                yield return CreateTuple(typeof(DeviceNotFoundException), typeof(DeviceNotFoundException));
                yield return CreateTuple(typeof(QuotaExceededException), typeof(QuotaExceededException));
                yield return CreateTuple(typeof(IotHubException), typeof(IotHubException));
                yield return CreateTuple(typeof(ObjectDisposedException), typeof(ObjectDisposedException));

                // Security Exceptions
                yield return CreateTuple(typeof(AuthenticationException), typeof(AuthenticationException));
                Func<Exception> innerSecurityExceptionFactory = () => new Exception(
                        "Test top level",
                        new Exception(
                            "Inner exception",
                            new AuthenticationException()));
                yield return new object[] { typeof(TestSecurityException), typeof(TestSecurityException), innerSecurityExceptionFactory };

                // Transient network exceptions
//                yield return CreateTuple(typeof(IotHubCommunicationException), typeof(IotHubCommunicationException));
//                yield return CreateTuple(typeof(IOException), typeof(IotHubCommunicationException));
//                yield return CreateTuple(typeof(TimeoutException), typeof(IotHubCommunicationException));
//                yield return CreateTuple(typeof(OperationCanceledException), typeof(IotHubCommunicationException));
//                yield return CreateTuple(typeof(TaskCanceledException), typeof(IotHubCommunicationException));
//                yield return CreateTuple(typeof(SocketException), typeof(IotHubCommunicationException));
//                yield return CreateTuple(typeof(HttpRequestException), typeof(IotHubCommunicationException));
//                yield return CreateTuple(typeof(WebException), typeof(IotHubCommunicationException));
//                yield return CreateTuple(typeof(WebSocketException), typeof(IotHubCommunicationException));
//                yield return CreateTuple(typeof(CodecException), typeof(IotHubCommunicationException));
//                // TODO: add MQTT and AMQP
//                //yield return CreateTuple(typeof(AmqpException), typeof(IotHubCommunicationException));
//
//
//                // Transient server exceptions
//                yield return CreateTuple(typeof(ServerBusyException), typeof(ServerBusyException));
//                yield return CreateTuple(typeof(IotHubThrottledException), typeof(IotHubThrottledException));
            }
        }

        private static object[] CreateTuple(Type triggerException, Type expectedException)
        {
            Func<Exception> factory = () => (Exception)Activator.CreateInstance(triggerException, ErrorMessage);

            return new object[] {
                expectedException,
                triggerException,
                factory
            };
        }

        public class TestSecurityException : Exception
        {
            public TestSecurityException() {}

            public TestSecurityException(string message) : base(message) {}

            public TestSecurityException(string message, Exception innerException) : base(message, innerException) {}
        }

        [TestMethod]
        public async Task ErrorHandler_NoErrors_Success()
        {
            var contextMock = Substitute.For<IPipelineContext>();
            var innerHandler = Substitute.For<IDelegatingHandler>();
            innerHandler.OpenAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
            innerHandler.SendEventAsync(Arg.Any<Message>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
            var sut = new ErrorDelegatingHandler(contextMock, innerHandler);

            var cancellationToken = new CancellationToken();
            await sut.OpenAsync(cancellationToken).ConfigureAwait(false);
            await sut.SendEventAsync(new Message(new byte[0]), cancellationToken).ConfigureAwait(false);

            await innerHandler.Received(1).OpenAsync(cancellationToken).ConfigureAwait(false);
            await innerHandler.Received(1).SendEventAsync(Arg.Any<Message>(), cancellationToken).ConfigureAwait(false);
        }
        
        [DataTestMethod]
        [DynamicData(nameof(TestMatrix), DynamicDataSourceType.Property)]
        public async Task ErrorHandler_ExceptionOccured_Ok(Type thrownExceptionType, Type expectedExceptionType, Func<Exception> exceptionFactory)
        {
            var message = new Message(new byte[0]);
            var cancellationToken = new CancellationToken();

            await OperationAsync_ExceptionThrownAndThenSucceed_OperationSuccessfullyCompleted(
                di => di.SendEventAsync(Arg.Is(message), Arg.Any<CancellationToken>()), 
                di => di.SendEventAsync(message, cancellationToken), 
                di => di.Received(2).SendEventAsync(Arg.Is(message), Arg.Any<CancellationToken>()), 
                thrownExceptionType, expectedExceptionType, exceptionFactory).ConfigureAwait(false);

            IEnumerable<Message> messages = new[] { new Message(new byte[0])};

            await OperationAsync_ExceptionThrownAndThenSucceed_OperationSuccessfullyCompleted(
                di => di.SendEventAsync(Arg.Is(message), Arg.Any<CancellationToken>()),
                di => di.SendEventAsync(message, cancellationToken),
                di => di.Received(2).SendEventAsync(Arg.Is(message), Arg.Any<CancellationToken>()),
                thrownExceptionType, expectedExceptionType, exceptionFactory).ConfigureAwait(false);

            await OpenAsync_ExceptionThrownAndThenSucceed_SuccessfullyOpened(
                di => di.OpenAsync(Arg.Any<CancellationToken>()),
                di => di.OpenAsync(cancellationToken),
                di => di.Received(2).OpenAsync(Arg.Any<CancellationToken>()),
                thrownExceptionType, expectedExceptionType, exceptionFactory).ConfigureAwait(false);

            string lockToken = "lockToken";

            await OperationAsync_ExceptionThrownAndThenSucceed_OperationSuccessfullyCompleted(
                di => di.CompleteAsync(Arg.Is(lockToken), Arg.Any<CancellationToken>()),
                di => di.CompleteAsync(lockToken, cancellationToken),
                di => di.Received(2).CompleteAsync(Arg.Is(lockToken), Arg.Any<CancellationToken>()),
                thrownExceptionType, expectedExceptionType, exceptionFactory).ConfigureAwait(false);

            await OperationAsync_ExceptionThrownAndThenSucceed_OperationSuccessfullyCompleted(
                di => di.AbandonAsync(Arg.Is(lockToken), Arg.Any<CancellationToken>()),
                di => di.AbandonAsync(lockToken, cancellationToken),
                di => di.Received(2).AbandonAsync(Arg.Is(lockToken), Arg.Any<CancellationToken>()),
                thrownExceptionType, expectedExceptionType, exceptionFactory).ConfigureAwait(false);

            await OperationAsync_ExceptionThrownAndThenSucceed_OperationSuccessfullyCompleted(
                di => di.RejectAsync(Arg.Is(lockToken), Arg.Any<CancellationToken>()),
                di => di.RejectAsync(lockToken, cancellationToken),
                di => di.Received(2).RejectAsync(Arg.Is(lockToken), Arg.Any<CancellationToken>()),
                thrownExceptionType, expectedExceptionType, exceptionFactory).ConfigureAwait(false);

            TimeSpan timeout = TimeSpan.FromSeconds(1);
            await OperationAsync_ExceptionThrownAndThenSucceed_OperationSuccessfullyCompleted(
                di => di.ReceiveAsync(Arg.Is(timeout), Arg.Any<CancellationToken>()),
                di => di.ReceiveAsync(timeout, cancellationToken),
                di => di.Received(2).ReceiveAsync(Arg.Is(timeout), Arg.Any<CancellationToken>()),
                thrownExceptionType, expectedExceptionType, exceptionFactory).ConfigureAwait(false);

            await OperationAsync_ExceptionThrownAndThenSucceed_OperationSuccessfullyCompleted(
                di => di.ReceiveAsync(Arg.Any<CancellationToken>()),
                di => di.ReceiveAsync(cancellationToken),
                di => di.Received(2).ReceiveAsync(Arg.Any<CancellationToken>()),
                thrownExceptionType, expectedExceptionType, exceptionFactory).ConfigureAwait(false);
        }

        static async Task OperationAsync_ExceptionThrownAndThenSucceed_OperationSuccessfullyCompleted(
            Func<IDelegatingHandler, Task<Message>> mockSetup, 
            Func<IDelegatingHandler, Task<Message>> act, 
            Func<IDelegatingHandler, Task<Message>> assert, 
            Type thrownExceptionType, 
            Type expectedExceptionType, 
            Func<Exception> exceptionFactory)
        {
            var contextMock = Substitute.For<IPipelineContext>();
            var innerHandler = Substitute.For<IDelegatingHandler>();
            var sut = new ErrorDelegatingHandler(contextMock, innerHandler);

            //initial OpenAsync to emulate Gatekeeper behavior
            var cancellationToken = new CancellationToken();
            innerHandler.OpenAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
            await sut.OpenAsync(cancellationToken).ConfigureAwait(false);

            //set initial operation result that throws

            bool[] setup = { false };
            mockSetup(innerHandler).Returns(ci =>
            {
                if (setup[0])
                {
                    return Task.FromResult(new Message());
                }

                throw exceptionFactory();
            });

            //act
            await ((Func<Task>)(() => act(sut))).ExpectedAsync(expectedExceptionType).ConfigureAwait(false);

            //override outcome
            setup[0] = true;//otherwise previously setup call will happen and throw;
            mockSetup(innerHandler).Returns(new Message());

            //act
            await act(sut).ConfigureAwait(false);

            //assert
            await innerHandler.Received(1).OpenAsync(Arg.Any<CancellationToken>()).ConfigureAwait(false);
            await assert(innerHandler).ConfigureAwait(false);
        }

        static async Task OperationAsync_ExceptionThrownAndThenSucceed_OperationSuccessfullyCompleted(
            Func<IDelegatingHandler, Task> mockSetup, 
            Func<IDelegatingHandler, Task> act, 
            Func<IDelegatingHandler, Task> assert, 
            Type thrownExceptionType, 
            Type expectedExceptionType, 
            Func<Exception> exceptionFactory)
        {
            var contextMock = Substitute.For<IPipelineContext>();
            var innerHandler = Substitute.For<IDelegatingHandler>();
            innerHandler.OpenAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
            var sut = new ErrorDelegatingHandler(contextMock, innerHandler);
            
            //initial OpenAsync to emulate Gatekeeper behavior
            var cancellationToken = new CancellationToken();
            await sut.OpenAsync(cancellationToken).ConfigureAwait(false);

            //set initial operation result that throws

            bool[] setup = { false };
            mockSetup(innerHandler).Returns(ci =>
            {
                if (setup[0])
                {
                    return Task.CompletedTask;
                }

                throw exceptionFactory();
            });

            //act
            await ((Func<Task>)(() => act(sut))).ExpectedAsync(expectedExceptionType).ConfigureAwait(false);

            //override outcome
            setup[0] = true;//otherwise previously setup call will happen and throw;
            mockSetup(innerHandler).Returns(Task.CompletedTask);

            //act
            await act(sut).ConfigureAwait(false);

            //assert
            await innerHandler.Received(1).OpenAsync(Arg.Any<CancellationToken>()).ConfigureAwait(false);
            await assert(innerHandler).ConfigureAwait(false);
        }

        static async Task OpenAsync_ExceptionThrownAndThenSucceed_SuccessfullyOpened(
            Func<IDelegatingHandler, Task> mockSetup, 
            Func<IDelegatingHandler, Task> act, 
            Func<IDelegatingHandler, Task> assert, 
            Type thrownExceptionType, 
            Type expectedExceptionType,
            Func<Exception> exceptionFactory)
        {
            var contextMock = Substitute.For<IPipelineContext>();
            var innerHandler = Substitute.For<IDelegatingHandler>();
            var sut = new ErrorDelegatingHandler(contextMock, innerHandler);

            //set initial operation result that throws

            bool[] setup = { false };
            mockSetup(innerHandler).Returns(ci =>
            {
                if (setup[0])
                {
                    return Task.FromResult(Guid.NewGuid());
                }

                throw exceptionFactory();
            });

            //act
            await ((Func<Task>)(() => act(sut))).ExpectedAsync(expectedExceptionType).ConfigureAwait(false);

            //override outcome
            setup[0] = true;//otherwise previously setup call will happen and throw;
            mockSetup(innerHandler).Returns(Task.CompletedTask);

            //act
            await act(sut).ConfigureAwait(false);

            //assert
            await assert(innerHandler).ConfigureAwait(false);
        }
    }
}
