<xsl:stylesheet version="1.0"
            xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
            xmlns:wix="http://wixtoolset.org/schemas/v4/wxs"
            exclude-result-prefixes="wix">

    <xsl:output method="xml" indent="yes" />

    <xsl:strip-space elements="*"/>

    <xsl:template match="@*|node()">
        <xsl:copy>
            <xsl:apply-templates select="@*|node()"/>
        </xsl:copy>
    </xsl:template>

    <xsl:template match="wix:Directory[@Name='AppHostTemplate']/wix:Component/wix:File">
        <xsl:copy>
            <xsl:attribute name="Id">apphosttemplateapphostexe</xsl:attribute>
            <xsl:apply-templates select="@*[name() != 'Id']|node()"/>
        </xsl:copy>
    </xsl:template>
</xsl:stylesheet>
