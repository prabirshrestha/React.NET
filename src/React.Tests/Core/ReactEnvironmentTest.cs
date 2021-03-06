﻿/*
 *  Copyright (c) 2014, Facebook, Inc.
 *  All rights reserved.
 *
 *  This source code is licensed under the BSD-style license found in the
 *  LICENSE file in the root directory of this source tree. An additional grant 
 *  of patent rights can be found in the PATENTS file in the same directory.
 */

using System;
using JavaScriptEngineSwitcher.Core;
using Moq;
using Xunit;

namespace React.Tests.Core
{
	public class ReactEnvironmentTest
	{
		[Fact]
		public void ShouldNotTransformJsxIfNoAnnotationPresent()
		{
			var mocks = new Mocks();
			var environment = mocks.CreateReactEnvironment();
			var input = "<div>Hello World</div>";

			var output = environment.TransformJsx(input);
			Assert.Equal(input, output);
		}

		[Fact]
		public void ShouldTransformJsxIfAnnotationPresent()
		{
			var mocks = new Mocks();
			var environment = mocks.CreateReactEnvironment();

			const string input = "/** @jsx React.DOM */ <div>Hello World</div>";
			environment.TransformJsx(input);

			mocks.Engine.Verify(x => x.Evaluate<string>(
				@"global.JSXTransformer.transform(""/** @jsx React.DOM */ <div>Hello World</div>"").code"
			));
		}

		[Fact]
		public void ExecuteWithLargerStackIfRequiredWithNoNewThread()
		{
			var mocks = new Mocks();
			var environment = mocks.CreateReactEnvironment();

			environment.ExecuteWithLargerStackIfRequired<int>("1+1");
			mocks.Engine.Verify(x => x.Evaluate<int>("1+1"));
		}

		[Fact]
		public void ExecuteWithLargerStackIfRequiredWithNewThread()
		{
			var mocks = new Mocks();
			var environment = mocks.CreateReactEnvironment();
			// Fail the first time Evaluate is called, succeed the second
			// http://stackoverflow.com/a/7045636
			mocks.Engine.Setup(x => x.Evaluate<int>("1+1"))
				.Callback(() => mocks.Engine.Setup(x => x.Evaluate<int>("1+1")))
				.Throws(new Exception("Out of stack space"));
				
			environment.ExecuteWithLargerStackIfRequired<int>("1+1");
			mocks.EngineFactory.Verify(
				x => x.GetEngineForCurrentThread(It.IsAny<Action<IJsEngine>>()), 
				Times.Exactly(2),
				"Two engines should be created (initial thread and new thread)"
			);
			mocks.EngineFactory.Verify(
				x => x.DisposeEngineForCurrentThread(), 
				Times.Exactly(1),
				"Inner engine should be disposed"
			);
		}

		[Fact]
		public void ExecuteWithLargerStackIfRequiredShouldBubbleExceptions()
		{
			var mocks = new Mocks();
			var environment = mocks.CreateReactEnvironment();
			// Always fail
			mocks.Engine.Setup(x => x.Evaluate<int>("1+1"))
				.Throws(new Exception("Something bad happened :("));

			Assert.Throws<Exception>(() =>
			{
				environment.ExecuteWithLargerStackIfRequired<int>("1+1");
			});
		}

		private class Mocks
		{
			public Mock<IJsEngine> Engine { get; private set; }
			public Mock<IJavaScriptEngineFactory> EngineFactory { get; private set; }
			public Mock<IReactSiteConfiguration> Config { get; private set; }
			public Mock<ICache> Cache { get; private set; }
			public Mock<IFileSystem> FileSystem { get; private set; }
			public Mocks()
			{
				Engine = new Mock<IJsEngine>();
				EngineFactory = new Mock<IJavaScriptEngineFactory>();
				Config = new Mock<IReactSiteConfiguration>();
				Cache = new Mock<ICache>();
				FileSystem = new Mock<IFileSystem>();

				EngineFactory.Setup(x => x.GetEngineForCurrentThread(It.IsAny<Action<IJsEngine>>())).Returns(Engine.Object);
			}

			public ReactEnvironment CreateReactEnvironment()
			{
				return new ReactEnvironment(
					EngineFactory.Object,
					Config.Object,
					Cache.Object,
					FileSystem.Object
				);
			}
		}
	}
}
