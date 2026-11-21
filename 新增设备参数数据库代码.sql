USE [PZP02ConsoleSQL]
GO

/****** Object:  Table [dbo].[fynT_equipmentParameter]    Script Date: 08/22/2022 14:37:41 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[fynT_equipmentParameter](
	[ID] [int] IDENTITY(1,1) NOT NULL,
	[equipmentNumber] [nvarchar](50) NOT NULL,
	[programNumber] [nvarchar](50) NULL,
	[programName] [nvarchar](200) NULL,
	[redactName] [nvarchar](50) NULL,
	[redactTime] [datetime] NULL,
	[versionNumber] [float] NULL,
 CONSTRAINT [PK_fynT_equipmentParameters] PRIMARY KEY CLUSTERED 
(
	[ID] ASC
)WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY],
 CONSTRAINT [IX_fynT_equipmentParameter] UNIQUE NONCLUSTERED 
(
	[equipmentNumber] ASC,
	[versionNumber] DESC
)WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY]
) ON [PRIMARY]

GO

ALTER TABLE [dbo].[fynT_equipmentParameter] ADD  CONSTRAINT [DF_fynT_equipmentParameter_redactTime]  DEFAULT (getdate()) FOR [redactTime]
GO

USE [PZP02ConsoleSQL]
GO

/****** Object:  Table [dbo].[fynT_equipmentParameters]    Script Date: 08/22/2022 14:37:46 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[fynT_equipmentParameters](
	[AutoID] [int] IDENTITY(1,1) NOT NULL,
	[ID] [int] NOT NULL,
	[parameterName] [nvarchar](200) NULL,
	[parameterValues] [nvarchar](50) NULL,
	[decimalPlace] [int] NULL,
	[PLCAddress] [nvarchar](50) NOT NULL,
	[dataType] [nvarchar](50) NULL,
 CONSTRAINT [PK_fynT_equipmentParameters_1] PRIMARY KEY CLUSTERED 
(
	[AutoID] ASC
)WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY]
) ON [PRIMARY]

GO

ALTER TABLE [dbo].[fynT_equipmentParameters]  WITH CHECK ADD  CONSTRAINT [FK_fynT_equipmentParameters_fynT_equipmentParameters] FOREIGN KEY([ID])
REFERENCES [dbo].[fynT_equipmentParameter] ([ID])
GO

ALTER TABLE [dbo].[fynT_equipmentParameters] CHECK CONSTRAINT [FK_fynT_equipmentParameters_fynT_equipmentParameters]
GO



USE [PZP02ConsoleSQL]
GO

/****** Object:  View [dbo].[fynV_EquipmentParameter]    Script Date: 08/22/2022 14:38:14 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO









CREATE view [dbo].[fynV_EquipmentParameter]
as
/*
**创建日期：2020-10-19
**创建人：付元念
**描述：设备参数视图
*/
select 
	b.AutoID
	,a.equipmentNumber
	,a.programName
	,a.programNumber
	,b.PLCAddress
	,b.parameterName
	,b.dataType
	,b.parameterValues
	,isnull(b.decimalPlace,0) as decimalPlace
	,value=case when  b.dataType<>'DInt' then  b.parameterValues
					else convert(nvarchar(50),convert(float,(convert(decimal,b.parameterValues)/(POWER(10,isnull(b.decimalPlace ,0))))))
					end
	,a.versionNumber
	,a.redactName
	,a.redactTime
	,d.category
from fynT_equipmentParameter a
left join fynT_equipmentParameters b on b.ID=a.ID
inner join (select equipmentNumber,programNumber,max(versionNumber) as versionNumber from fynT_equipmentParameter group by equipmentNumber,programNumber) c on 
c.equipmentNumber=a.equipmentNumber and c.programNumber=a.programNumber and c.versionNumber=a.versionNumber --and c.programName=a.programName
left join [dbo].[fynT_PLCcommunicateAddress] d on d.address=b.PLCAddress and d.PLCstationNum in (select [PLCstationNum] from [dbo].[fynT_PLCaddress] where [equipmentNumber]=a.equipmentNumber)
where isnull(b.parameterName,'')<>'' and d.category='0104'



GO



USE [PZP02ConsoleSQL]
GO

/****** Object:  StoredProcedure [dbo].[fynP_saveEquipmentParameterExecl]    Script Date: 08/22/2022 14:39:03 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO







CREATE proc [dbo].[fynP_saveEquipmentParameterExecl]
@equipmentNumber nvarchar(50)='210 钳体上线' --设备编号
,@programNumber nvarchar(50)='' --程序号
,@programName nvarchar(50)='' --程序名称
,@redactName nvarchar(50)='admin' --编辑人
as
/*
**时间：2022-02-15
**姓名：fyn
**描述：画面上修改参数，添加参数保存数据。并生成新的参数版本号
*/
declare @PLCstationNum int --PLC站号
,@equipment nvarchar(50)
--获取plc站号
--select @PLCstationNum=PLCstationNum from fynT_PLCaddress where [equipmentNumber]=@equipmentNumber
select @PLCstationNum=PLCstationNum,@equipment=equipmentNumber from fynT_PLCaddress where [PLCname]=@equipmentNumber
--获取保存数据
IF EXISTS(select * from tempdb..sysobjects where id=object_id('tempdb..#tempvalue'))
begin
	print '临时表#tempvalue存在'
	drop table #tempvalue
end
select a.programNumber 
		,a.programName
		,a.PLCstationNum
		,a.equipmentNumber
		,redactName
		,redactTime
		,a.PLCAddress
		,parameterValues=case when a.category='0101' then a.programNumber
								when a.category='0102' then a.programName
								else convert(nvarchar(50),a.parameterValues)
								end
		,a.AddressAnnotation
		,a.category
into #tempvalue
from 
(
select @programNumber as programNumber
		,@programName as programName
		,@PLCstationNum as PLCstationNum 
		--,@equipmentNumber as equipmentNumber
		,@equipment as equipmentNumber
		,@redactName as redactName
		,getdate() as redactTime
		,b.[address] as PLCAddress
		,parameterValues= case when b.[dataType]='DInt' then convert(float,a.parameterValues)*power(10,b.[decimalPlaces])
								when b.[dataType]='Real' then a.parameterValues
								when b.[dataType]='String' then a.parameterValues
								end
		,b.AddressAnnotation
		,b.category
from [dbo].[fynT_PLCcommunicateAddress] b
left join dbo.fynT_TemporaryParametersExecl a on a.PLCAddress =b.[address]
where  b.category like '0104%' and b.PLCstationNum=@PLCstationNum 
) a


SELECT * FROM #tempvalue

--获取参数版本号
declare @maxVersionNumber float --最大参数版本号
		,@nextVersionNumber float --下一参数版本号
select @maxVersionNumber=isnull(max(isnull(versionNumber,0)),0)  from [dbo].[fynT_equipmentParameter] where equipmentNumber in (select equipmentNumber from #tempvalue) and programNumber in (select programNumber from #tempvalue)
set @nextVersionNumber=@maxVersionNumber+0.1
print '下一参数版本号：V'+ convert(nvarchar(10),@nextVersionNumber)
--获取程序号、程序名称、设备编号
select top 1 equipmentNumber,programNumber,programName,redactName,redactTime,@nextVersionNumber as nextVersionNumber from #tempvalue
--插入参数主表
insert into [dbo].[fynT_equipmentParameter] ([equipmentNumber],[programNumber],[programName],[redactName],[redactTime],[versionNumber])
select top 1 equipmentNumber,programNumber,programName,redactName,redactTime,@nextVersionNumber from #tempvalue
--获取参数主表ID
declare @id int --参数主表ID
select top 1 @id=ID  from [dbo].[fynT_equipmentParameter] where equipmentNumber in (select equipmentNumber from #tempvalue) and programNumber in (select programNumber from #tempvalue) order by versionNumber desc
print '参数主表ID：'+convert(nvarchar(10),@id)
--插入参数子表
insert into [dbo].[fynT_equipmentParameters] ([ID],[parameterName],[parameterValues],[decimalPlace],[PLCAddress],[dataType])
select @id
		,b.AddressAnnotation
		,a.parameterValues
		,b.decimalPlaces
		,a.PLCAddress
		,b.dataType
from #tempvalue a
left join [dbo].[fynT_PLCcommunicateAddress] b on b.address=a.PLCAddress and b.PLCstationNum=a.PLCstationNum
where b.category='0104'
order by b.ID


select * from [dbo].[fynT_equipmentParameter] a
left join [dbo].[fynT_equipmentParameters] b on b.ID=a.ID
where a.versionNumber=@nextVersionNumber



/*
delete fynT_parameters 
delete fynT_parameter
*/

--select max(isnull(versionNumber,0)) from [dbo].[fynT_parameter] where equipmentNumber in (select equipmentNumber from #tempvalue) and programNumber in (select programNumber from #tempvalue)




GO


USE [PZP02ConsoleSQL]
GO

/****** Object:  StoredProcedure [dbo].[fynP_parameter]    Script Date: 08/22/2022 14:40:10 ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO







ALTER proc [dbo].[fynP_parameter]
@PLCstationNum int=1
,@programNumber int=16
as
begin
--drop table #a
--drop table #b
	--declare @PLCstationNum int=1
	--		,@programNumber int=6
	declare @equipmentNumber nvarchar(50)
			,@programName nvarchar(50)
	select *
	into #a 
	from fynT_PLCcommunicateAddress 
	where left(category,2)='01' and PLCstationNum=@PLCstationNum
	--select * from #a
	select @equipmentNumber=equipmentNumber from fynT_PLCaddress where PLCstationNum=@PLCstationNum
	
	select *
	into #b1
	from fynV_parameter 
	where programNumber=@programNumber and equipmentNumber=@equipmentNumber
	
	select *
	into #b2
	from fynV_EquipmentParameter
	where equipmentNumber=@equipmentNumber

	select top 1 @programName=programName from #b1
	
	select * into #b from(
	select * from #b1
	union
	select * from #b2) a
	
	select a.ID
			,a.category
			,a.PLCstationNum
			,a.address
			,a.AddressAnnotation
			,a.dataType
			,a.decimalPlaces
			,parameterValues=case when a.dataType='String' and b.parameterValues is null then ''
									when a.dataType<>'String' and b.parameterValues is null then '0'
									else b.parameterValues end 
			,sendparameterValues=case when a.category='0101' then convert(nvarchar(50),@programNumber)
									when a.category='0102' then @programName
									else (case when a.dataType='String' and b.parameterValues is null then ''
									when a.dataType<>'String' and b.parameterValues is null then '0'
									else b.parameterValues end )
									end
			,value=case when a.dataType='String' and b.value is null then ''
									when a.dataType<>'String' and b.value is null then '0'
									else b.value end 
			,b.equipmentNumber
			,b.programName
			,b.programNumber
			,dbo.GET_NUMBER(address) as addnum
			,a.workNumber
	from #a a
	left join #b b on b.PLCAddress=a.address
	--where a.category='0103'
	order by a.ID,a.address
end





GO


