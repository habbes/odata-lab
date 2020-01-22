﻿using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.VisualStudio.ConnectedServices;
using EnvDTE;
using Moq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.OData.ConnectedService;
using Microsoft.OData.ConnectedService.CodeGeneration;
using Microsoft.OData.ConnectedService.Models;
using Microsoft.OData.ConnectedService.Templates;
using Microsoft.VisualStudio.Shell.Interop;

namespace ODataConnectedService.Tests.CodeGeneration
{
    [TestClass]
    public class V4CodeGenDescriptorTest
    {
        readonly static string TestProjectRootPath = Path.Combine(Directory.GetCurrentDirectory(), "TempODataConnectedServiceTest");
        readonly static string ServicesRootFolder = "ConnectedServicesRoot";
        readonly static string MetadataUri = "http://service/$metadata";

        [TestCleanup]
        public void CleanUp()
        {
            try
            {
                Directory.Delete(TestProjectRootPath, true);
            }
            catch (DirectoryNotFoundException) { }
        }

        [TestMethod]
        public void TestAddGeneratedClientCode_PassesServiceConfigOptionsToCodeGenerator()
        {
            var handlerHelper = new TestConnectedServiceHandlerHelper();
            var codeGenFactory = new TestODataT4CodeGeneratorFactory();

            var serviceConfig = new ServiceConfigurationV4()
            {
                UseDataServiceCollection = true,
                IgnoreUnexpectedElementsAndAttributes = true,
                EnableNamingAlias = true,
                NamespacePrefix = "Prefix",
                MakeTypesInternal = true,
                IncludeT4File = false
            };
            var codeGenDescriptor = SetupCodeGenDescriptor(serviceConfig, "TestService", codeGenFactory, handlerHelper);
            codeGenDescriptor.AddGeneratedClientCode().Wait();
            var generator = codeGenFactory.LastCreatedInstance;
            Assert.AreEqual(true, generator.UseDataServiceCollection);
            Assert.AreEqual(true, generator.EnableNamingAlias);
            Assert.AreEqual(true, generator.MakeTypesInternal);
            Assert.AreEqual("Prefix", generator.NamespacePrefix);
            Assert.AreEqual(MetadataUri, generator.MetadataDocumentUri);
            Assert.AreEqual(ODataT4CodeGenerator.LanguageOption.CSharp, generator.TargetLanguage);

            serviceConfig = new ServiceConfigurationV4()
            {
                UseDataServiceCollection = false,
                IgnoreUnexpectedElementsAndAttributes = false,
                EnableNamingAlias = false,
                NamespacePrefix = null,
                MakeTypesInternal = false,
                IncludeT4File = false
            };
            codeGenDescriptor = SetupCodeGenDescriptor(serviceConfig, "TestService", codeGenFactory, handlerHelper);
            codeGenDescriptor.AddGeneratedClientCode().Wait();
            generator = codeGenFactory.LastCreatedInstance;
            Assert.AreEqual(false, generator.UseDataServiceCollection);
            Assert.AreEqual(false, generator.EnableNamingAlias);
            Assert.AreEqual(false, generator.MakeTypesInternal);
            Assert.AreEqual(null, generator.NamespacePrefix);
            Assert.AreEqual(MetadataUri, generator.MetadataDocumentUri);
            Assert.AreEqual(ODataT4CodeGenerator.LanguageOption.CSharp, generator.TargetLanguage);
        }

        [TestMethod]
        public void TestAddGeneratedClientCode_GeneratesAndSavesCodeFile()
        {
            var serviceName = "MyService";
            ServiceConfiguration serviceConfig = new ServiceConfigurationV4()
            {
                MakeTypesInternal = true,
                UseDataServiceCollection = false,
                ServiceName = serviceName,
                GeneratedFileNamePrefix = "MyFile",
                IncludeT4File = false
            };
            var handlerHelper = new TestConnectedServiceHandlerHelper();
            var codeGenDescriptor = SetupCodeGenDescriptor(serviceConfig, serviceName,
                new TestODataT4CodeGeneratorFactory(), handlerHelper);
            codeGenDescriptor.AddGeneratedClientCode().Wait();
            using (var reader = new StreamReader(handlerHelper.AddedFileInputFileName))
            {
                var generatedCode = reader.ReadToEnd();
                Assert.AreEqual("Generated code", generatedCode);
                Assert.AreEqual(Path.Combine(TestProjectRootPath, ServicesRootFolder, serviceName, "MyFile.cs"),
                    handlerHelper.AddedFileTargetFilePath);
            }
        }

        static V4CodeGenDescriptor SetupCodeGenDescriptor(ServiceConfiguration serviceConfig, string serviceName, IODataT4CodeGeneratorFactory codeGenFactory, TestConnectedServiceHandlerHelper handlerHelper)
        {
            var referenceFolderPath = Path.Combine(TestProjectRootPath, ServicesRootFolder, serviceName);
            Directory.CreateDirectory(referenceFolderPath);
            Project project = CreateTestProject(TestProjectRootPath);
            var serviceInstance = new ODataConnectedServiceInstance()
            {
                ServiceConfig = serviceConfig,
                Name = serviceName
            };
            handlerHelper.ServicesRootFolder = ServicesRootFolder;
            ConnectedServiceHandlerContext context = new TestConnectedServiceHandlerContext(serviceInstance, handlerHelper);

            return new TestV4CodeGenDescriptor(MetadataUri, context, project, codeGenFactory);
        }

        static Project CreateTestProject(string projectPath)
        {
            var fullPathPropertyMock = new Mock<Property>();
            fullPathPropertyMock.SetupGet(p => p.Value).Returns(projectPath);
            var projectPropertiesMock = new Mock<Properties>();
            projectPropertiesMock.Setup(p => p.Item(It.Is<string>(s => s == "FullPath")))
                .Returns(fullPathPropertyMock.Object);
            var projectMock = new Mock<Project>();
            projectMock.SetupGet(p => p.Properties)
                .Returns(projectPropertiesMock.Object);
            return projectMock.Object;
        }
    }

    class TestV4CodeGenDescriptor: V4CodeGenDescriptor
    {
        public TestV4CodeGenDescriptor(string metadataUri, ConnectedServiceHandlerContext context, Project project, IODataT4CodeGeneratorFactory codeGenFactory)
            : base(metadataUri, context, project, codeGenFactory)
        {
        }
        protected override void Init() { }
    }

    class TestODataT4CodeGenerator: ODataT4CodeGenerator
    {
        public override string TransformText()
        {
            return "Generated code";
        }
    }

    class TestODataT4CodeGeneratorFactory: IODataT4CodeGeneratorFactory
    {
        public ODataT4CodeGenerator LastCreatedInstance { get; private set; }
        public ODataT4CodeGenerator Create()
        {
            var generator = new TestODataT4CodeGenerator();
            LastCreatedInstance = generator;
            return generator;
        }
    }

    class TestConnectedServiceHandlerHelper: ConnectedServiceHandlerHelper
    {
        // used to access the temp file that the generated code was written to
        public string AddedFileInputFileName { get; private set; }
        // used to find out which file the final output would be written to
        public string AddedFileTargetFilePath { get; private set; }
        public string ServicesRootFolder { get; set; }
        public override IDictionary<string, string> TokenReplacementValues { get; }
        public override void AddAssemblyReference(string assemblyPath) { }
        public override string GetServiceArtifactsRootFolder() => ServicesRootFolder;
        public override string PerformTokenReplacement(string input, IDictionary<string, string> additionalReplacementValues = null) => "";

        public override Task<string> AddFileAsync(string fileName, string targetPath, AddFileOptions addFileOptions = null)
        {
            AddedFileInputFileName = fileName;
            AddedFileTargetFilePath = targetPath;
            return Task.FromResult("");
        }
    }
    
    class TestConnectedServiceHandlerContext: ConnectedServiceHandlerContext
    {
        public TestConnectedServiceHandlerContext(ConnectedServiceInstance serviceInstance = null,
            ConnectedServiceHandlerHelper handlerHelper = null, IVsHierarchy projectHierarchy = null ): base()
        {
            ServiceInstance = serviceInstance;
            HandlerHelper = handlerHelper;
            ProjectHierarchy = projectHierarchy;

            var mockLogger = new Mock<ConnectedServiceLogger>();
            mockLogger.Setup(l => l.WriteMessageAsync(It.IsAny<LoggerMessageCategory>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);
            Logger = mockLogger.Object;
        }

        public object SavedExtendedDesignData { get; private set; }
        public override IDictionary<string, object> Args => throw new System.NotImplementedException();

        public override EditableXmlConfigHelper CreateEditableXmlConfigHelper()
        {
            return null;
        }

        public override XmlConfigHelper CreateReadOnlyXmlConfigHelper()
        {
            throw new System.NotImplementedException();
        }

        public override TData GetExtendedDesignerData<TData>()
        {
            throw new System.NotImplementedException();
        }

        public override void SetExtendedDesignerData<TData>(TData data)
        {
            SavedExtendedDesignData = data;
        }
    }
}
