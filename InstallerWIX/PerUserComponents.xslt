<?xml version="1.0" encoding="utf-8"?>
<!-- Applied to the components harvested by heat. The files go to the user profile, so the key path
     of every component must be a registry value in HKCU, not the file itself (ICE38). -->
<xsl:stylesheet version="1.0"
                xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
                xmlns:wix="http://schemas.microsoft.com/wix/2006/wi"
                xmlns="http://schemas.microsoft.com/wix/2006/wi"
                exclude-result-prefixes="wix">
  <xsl:output method="xml" indent="yes" encoding="utf-8" />

  <xsl:template match="@*|node()">
    <xsl:copy>
      <xsl:apply-templates select="@*|node()" />
    </xsl:copy>
  </xsl:template>

  <xsl:template match="wix:Component">
    <xsl:copy>
      <xsl:apply-templates select="@*|node()" />
      <RegistryValue Root="HKCU" Key="Software\RevitNumbering\Components" Name="{@Id}" Type="integer" Value="1" KeyPath="yes" />
    </xsl:copy>
  </xsl:template>

  <xsl:template match="wix:File/@KeyPath" />
</xsl:stylesheet>
