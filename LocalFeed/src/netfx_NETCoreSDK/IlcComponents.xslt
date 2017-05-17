<?xml version="1.0" encoding="utf-8"?>
<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
	xmlns:wix="http://schemas.microsoft.com/wix/2006/wi"
  xmlns:netfx="http://schemas.microsoft.com/wix/NetFxExtension">

  <xsl:output method="xml" encoding="UTF-8" indent="yes"/>
  
  <xsl:param name="excludelist" select="document('excludelist.xml')/ExcludeList/Assembly"/>
  
  <xsl:template match="wix:Wix">
    <xsl:copy>
      <xsl:apply-templates select="@*|node()" />
    </xsl:copy>
  </xsl:template>

  <!-- NGEN .dll and .exe files in ilc/tools -->
  <xsl:template match="wix:Directory[@Name='Microsoft.Net.Native.Compiler']/wix:Directory[@Name='1.6.0']/wix:Directory[@Name='tools']/wix:Directory/wix:Directory[@Name='ilc']/wix:Directory[@Name='tools']/wix:Component/wix:File[substring(@Source,string-length(@Source)-3) = '.dll' or substring(@Source,string-length(@Source)-3) = '.exe']">
    <xsl:copy>
      <xsl:apply-templates select="@*" />
      
      <xsl:variable name="source">
        <xsl:call-template name="fileNameFromPath">
           <xsl:with-param name="string" select="@Source" />
        </xsl:call-template>
      </xsl:variable>
      
      <xsl:if test="not(count($excludelist[@File = $source]))">
        <netfx:NativeImage Priority="3" Platform="32bit">
          <xsl:attribute name="Id">
            <xsl:value-of select="concat(@Id, 'NGEN')"/>
          </xsl:attribute>
        </netfx:NativeImage>
      </xsl:if>
    </xsl:copy>
  </xsl:template>
  
  <!-- NGEN .dll and .exe files in ilc/csc -->
  <xsl:template match="wix:Directory[@Name='Microsoft.Net.Native.Compiler']/wix:Directory[@Name='1.6.0']/wix:Directory[@Name='tools']/wix:Directory/wix:Directory[@Name='ilc']/wix:Directory[@Name='csc']/wix:Component/wix:File[substring(@Source,string-length(@Source)-3) = '.dll' or substring(@Source,string-length(@Source)-3) = '.exe']">
    <xsl:copy>
      <xsl:apply-templates select="@*" />

      <xsl:variable name="source">
        <xsl:call-template name="fileNameFromPath">
          <xsl:with-param name="string" select="@Source" />
        </xsl:call-template>
      </xsl:variable>

      <xsl:if test="not(count($excludelist[@File = $source]))">
        <netfx:NativeImage Priority="3">
          <xsl:attribute name="Id">
            <xsl:value-of select="concat(@Id, 'NGEN')"/>
          </xsl:attribute>
        </netfx:NativeImage>
      </xsl:if>
    </xsl:copy>
  </xsl:template>

  <!-- NGEN .dll and .exe files in ilc/ -->
  <xsl:template match="wix:Directory[@Name='Microsoft.Net.Native.Compiler']/wix:Directory[@Name='1.6.0']/wix:Directory[@Name='tools']/wix:Directory/wix:Directory[@Name='ilc']/wix:Component/wix:File[substring(@Source,string-length(@Source)-3) = '.dll' or substring(@Source,string-length(@Source)-3) = '.exe']">
    <xsl:copy>
      <xsl:apply-templates select="@*" />
      <netfx:NativeImage Priority="3" Platform="32bit">
        <xsl:attribute name="Id">
          <xsl:value-of select="concat(@Id, 'NGEN')"/>
        </xsl:attribute>
      </netfx:NativeImage>
    </xsl:copy>
  </xsl:template>

  <xsl:template match="@*|node()">
    <xsl:copy>
      <xsl:apply-templates select="@*|node()" />
    </xsl:copy>
  </xsl:template>

  <xsl:template name="fileNameFromPath">
     <xsl:param name="string" />
     <xsl:variable name="separator" select="'\'"/>
     <xsl:choose>
        <xsl:when test="contains($string, $separator)">
           <xsl:call-template name="fileNameFromPath">
              <xsl:with-param name="string" select="substring-after($string, $separator)" />
           </xsl:call-template>
        </xsl:when>
        <xsl:otherwise>
          <xsl:value-of select="$string" />
        </xsl:otherwise>
     </xsl:choose>
  </xsl:template>
</xsl:stylesheet>  