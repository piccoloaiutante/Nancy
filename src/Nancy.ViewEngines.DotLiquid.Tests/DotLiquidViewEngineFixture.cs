﻿namespace Nancy.ViewEngines.DotLiquid.Tests
{
    using System;
    using System.IO;
    using FakeItEasy;
    using global::DotLiquid;
    using Nancy.Tests;
    using Xunit;
    using System.Collections.Generic;

    public class DotLiquidViewEngineFixture
    {
        private readonly IRenderContext renderContext;
        private readonly IFileSystemFactory factory;
        private readonly DotLiquidViewEngine engine;

        public DotLiquidViewEngineFixture()
        {
            this.factory = A.Fake<IFileSystemFactory>();
            this.engine = new DotLiquidViewEngine(this.factory);

            var cache = A.Fake<IViewCache>();
            A.CallTo(() => cache.GetOrAdd(A<ViewLocationResult>.Ignored, A<Func<ViewLocationResult, Template>>.Ignored))
                .ReturnsLazily(x =>
                {
                    var result = x.GetArgument<ViewLocationResult>(0);
                    return x.GetArgument<Func<ViewLocationResult, Template>>(1).Invoke(result);
                });
            var context = new NancyContext();

            this.renderContext = A.Fake<IRenderContext>();
            A.CallTo(() => this.renderContext.ViewCache).Returns(cache);
            A.CallTo(() => this.renderContext.Context).Returns(context);
        }

        [Fact]
        public void Should_retrieve_filesystem_from_factory_when_engine_is_initialized()
        {
            // Given
            var context = CreateContext();

            // When
            this.engine.Initialize(context);

            // Then
            A.CallTo(() => factory.GetFileSystem(context)).MustHaveHappened();
        }

        [Fact]
        public void Should_support_files_with_the_liquid_extensions()
        {
            // Given
            // When
            var extensions = engine.Extensions;

            // Then
            extensions.ShouldHaveCount(1);
            extensions.ShouldEqualSequence(new[] { "liquid" });
        }

        [Fact]
        public void RenderView_should_render_to_stream()
        {
            // Given
            var location = new ViewLocationResult(
                string.Empty,
                string.Empty,
                "liquid",
                () => new StringReader(@"{% assign name = 'test' %}<h1>Hello Mr. {{ name }}</h1>")
            );

            var currentStartupContext =
                CreateContext(new[] { location });

            this.engine.Initialize(currentStartupContext);

            var stream = new MemoryStream();

            // When
            var response = this.engine.RenderView(location, null, this.renderContext);
            response.Contents.Invoke(stream);

            // Then
            stream.ShouldEqual("<h1>Hello Mr. test</h1>");
        }

        [Fact]
        public void When_passing_a_null_model_should_return_an_empty_string()
        {
            // Given
            var location = new ViewLocationResult(
                string.Empty,
                string.Empty,
                "liquid",
                () => new StringReader(@"<h1>Hello Mr. {{ Model.name }}</h1>")
            );

            var currentStartupContext =
                CreateContext(new[] { location });

            this.engine.Initialize(currentStartupContext);

            var stream = new MemoryStream();

            // When
            var response = engine.RenderView(location, null, this.renderContext);
            response.Contents.Invoke(stream);

            // Then
            stream.ShouldEqual("<h1>Hello Mr. </h1>");
        }

        [Fact]
        public void RenderView_should_accept_a_model_and_read_from_it_into_the_stream()
        {
            // Given
            var location = new ViewLocationResult(
                string.Empty,
                string.Empty,
                "liquid",
                () => new StringReader(@"<h1>Hello Mr. {{ Model.name }}</h1>")
            );

            var currentStartupContext =
                CreateContext(new[] { location });

            this.engine.Initialize(currentStartupContext);
            var stream = new MemoryStream();

            // When
            var response = this.engine.RenderView(location, new { name = "test" }, this.renderContext);
            response.Contents.Invoke(stream);

            // Then
            stream.ShouldEqual("<h1>Hello Mr. test</h1>");
        }

        [Fact]
        public void RenderView_should_expose_ViewBag_to_the_template()
        {
            // Given
            var location = new ViewLocationResult(
                string.Empty,
                string.Empty,
                "liquid",
                () => new StringReader(@"<h1>Hello Mr. {{ ViewBag.Name }}</h1>")
            );

            var currentStartupContext =
                CreateContext(new[] { location });

            this.engine.Initialize(currentStartupContext);
            var stream = new MemoryStream();
            this.renderContext.Context.ViewBag.Name = "test";

            // When
            var response = this.engine.RenderView(location, new { name = "incorrect" }, this.renderContext);
            response.Contents.Invoke(stream);

            // Then
            stream.ShouldEqual("<h1>Hello Mr. test</h1>");
        }

        [Fact]
        public void when_calling_a_missing_member_should_return_an_empty_string()
        {
            // Given
            var location = new ViewLocationResult(
                string.Empty,
                string.Empty,
                "liquid",
                () => new StringReader(@"<h1>Hello Mr. {{ Model.name }}</h1>")
            );

            var currentStartupContext =
                CreateContext(new[] { location });

            this.engine.Initialize(currentStartupContext);
            var stream = new MemoryStream();

            // When
            var response = this.engine.RenderView(location, new { lastname = "test" }, this.renderContext);
            response.Contents.Invoke(stream);

            // Then
            stream.ShouldEqual("<h1>Hello Mr. </h1>");
        }

        [Fact]
        public void When_rendering_model_inheriting_drop_should_preserve_camel_case()
        {
            // Writing the test name is snake_case is slightly ironic, no?

            // Given
            var location = new ViewLocationResult(
                string.Empty,
                string.Empty,
                "liquid",
                () => new StringReader(@"{{ Model.CamelCase }}")
            );

            var currentStartupContext =
                CreateContext(new[] { location });

            this.engine.Initialize(currentStartupContext);
            var stream = new MemoryStream();

            // When
            var dropModel = new DropModel() { CamelCase = "Hello Jamie!" };
            var response = this.engine.RenderView(location, dropModel, this.renderContext);
            response.Contents.Invoke(stream);

            // Then
            stream.ShouldEqual("Hello Jamie!");
        }

        [Fact]
        public void RenderView_should_accept_a_model_with_a_list_and_iterate_over_it()
        {
            // Given
            var location = new ViewLocationResult(
                string.Empty,
                string.Empty,
                "liquid",
                () => new StringReader(@"<ul>{% for item in Model.Items %}<li>{{ item.Name }}</li>{% endfor %}</ul>")
            );

            var currentStartupContext =
                CreateContext(new[] { location });

            this.engine.Initialize(currentStartupContext);
            var stream = new MemoryStream();

            // Construct the model for the View
            IList<Article> articles = new List<Article>() {
                new Article() {Name = "Hello"},
                new Article() {Name = "Jamie!"},
                new Article() {Name = "You're fun!"}
            };

            Magazine menu = new Magazine() { Items = articles };

            // When
            var response = this.engine.RenderView(location, menu, this.renderContext);
            response.Contents.Invoke(stream);

            // Then
            stream.ShouldEqual("<ul><li>Hello</li><li>Jamie!</li><li>You're fun!</li></ul>");
        }

        private ViewEngineStartupContext CreateContext(params ViewLocationResult[] results)
        {
            return new ViewEngineStartupContext(
                this.renderContext.ViewCache,
                results,
                new[] { "liquid" });
        }
    }

    public class Magazine
    {
        public IList<Article> Items { get; set; }
    }

    public class Article : Drop
    {
        public string Name { get; set; }
    }

    public class DropModel : Drop
    {
        public string CamelCase { get; set; }
    }
}
