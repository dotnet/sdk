
// NOTE: Generated code may require at least .NET Framework 4.5 or .NET Core/Standard 2.0.
/// <remarks/>
[System.SerializableAttribute()]
[System.ComponentModel.DesignerCategoryAttribute("code")]
[System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true)]
[System.Xml.Serialization.XmlRootAttribute(Namespace = "", IsNullable = false)]
public partial class ScenarioBenchmark
{

    private ScenarioBenchmarkTest[] testsField;

    private string nameField;

    private string namespaceField;

    /// <remarks/>
    [System.Xml.Serialization.XmlArrayItemAttribute("Test", IsNullable = false)]
    public ScenarioBenchmarkTest[] Tests
    {
        get
        {
            return this.testsField;
        }
        set
        {
            this.testsField = value;
        }
    }

    /// <remarks/>
    [System.Xml.Serialization.XmlAttributeAttribute()]
    public string Name
    {
        get
        {
            return this.nameField;
        }
        set
        {
            this.nameField = value;
        }
    }

    /// <remarks/>
    [System.Xml.Serialization.XmlAttributeAttribute()]
    public string Namespace
    {
        get
        {
            return this.namespaceField;
        }
        set
        {
            this.namespaceField = value;
        }
    }
}

/// <remarks/>
[System.SerializableAttribute()]
[System.ComponentModel.DesignerCategoryAttribute("code")]
[System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true)]
public partial class ScenarioBenchmarkTest
{

    private string separatorField;

    private ScenarioBenchmarkTestPerformance performanceField;

    private string nameField;

    private string namespaceField;

    /// <remarks/>
    public string Separator
    {
        get
        {
            return this.separatorField;
        }
        set
        {
            this.separatorField = value;
        }
    }

    /// <remarks/>
    public ScenarioBenchmarkTestPerformance Performance
    {
        get
        {
            return this.performanceField;
        }
        set
        {
            this.performanceField = value;
        }
    }

    /// <remarks/>
    [System.Xml.Serialization.XmlAttributeAttribute()]
    public string Name
    {
        get
        {
            return this.nameField;
        }
        set
        {
            this.nameField = value;
        }
    }

    /// <remarks/>
    [System.Xml.Serialization.XmlAttributeAttribute()]
    public string Namespace
    {
        get
        {
            return this.namespaceField;
        }
        set
        {
            this.namespaceField = value;
        }
    }
}

/// <remarks/>
[System.SerializableAttribute()]
[System.ComponentModel.DesignerCategoryAttribute("code")]
[System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true)]
public partial class ScenarioBenchmarkTestPerformance
{

    private ScenarioBenchmarkTestPerformanceMetrics metricsField;

    private ScenarioBenchmarkTestPerformanceIteration[] iterationsField;

    /// <remarks/>
    public ScenarioBenchmarkTestPerformanceMetrics metrics
    {
        get
        {
            return this.metricsField;
        }
        set
        {
            this.metricsField = value;
        }
    }

    /// <remarks/>
    [System.Xml.Serialization.XmlArrayItemAttribute("iteration", IsNullable = false)]
    public ScenarioBenchmarkTestPerformanceIteration[] iterations
    {
        get
        {
            return this.iterationsField;
        }
        set
        {
            this.iterationsField = value;
        }
    }
}

/// <remarks/>
[System.SerializableAttribute()]
[System.ComponentModel.DesignerCategoryAttribute("code")]
[System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true)]
public partial class ScenarioBenchmarkTestPerformanceMetrics
{

    private ScenarioBenchmarkTestPerformanceMetricsExecutionTime executionTimeField;

    /// <remarks/>
    public ScenarioBenchmarkTestPerformanceMetricsExecutionTime ExecutionTime
    {
        get
        {
            return this.executionTimeField;
        }
        set
        {
            this.executionTimeField = value;
        }
    }
}

/// <remarks/>
[System.SerializableAttribute()]
[System.ComponentModel.DesignerCategoryAttribute("code")]
[System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true)]
public partial class ScenarioBenchmarkTestPerformanceMetricsExecutionTime
{

    private string displayNameField;

    private string unitField;

    /// <remarks/>
    [System.Xml.Serialization.XmlAttributeAttribute()]
    public string displayName
    {
        get
        {
            return this.displayNameField;
        }
        set
        {
            this.displayNameField = value;
        }
    }

    /// <remarks/>
    [System.Xml.Serialization.XmlAttributeAttribute()]
    public string unit
    {
        get
        {
            return this.unitField;
        }
        set
        {
            this.unitField = value;
        }
    }
}

/// <remarks/>
[System.SerializableAttribute()]
[System.ComponentModel.DesignerCategoryAttribute("code")]
[System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true)]
public partial class ScenarioBenchmarkTestPerformanceIteration
{

    private byte indexField;

    private decimal executionTimeField;

    /// <remarks/>
    [System.Xml.Serialization.XmlAttributeAttribute()]
    public byte index
    {
        get
        {
            return this.indexField;
        }
        set
        {
            this.indexField = value;
        }
    }

    /// <remarks/>
    [System.Xml.Serialization.XmlAttributeAttribute()]
    public decimal ExecutionTime
    {
        get
        {
            return this.executionTimeField;
        }
        set
        {
            this.executionTimeField = value;
        }
    }
}

