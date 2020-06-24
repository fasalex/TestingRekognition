---
title: ASP.NET Core application to upload images via AWS API Gateway
published: false
description: ASP.NET Core application to upload images via API Gateway to AWS Lambda for image recognition using AWS Rekognition's API
tags: awslambda, awsapigateway, aspnetcore, rekognition
//cover_image: https://dev-to-uploads.s3.amazonaws.com/i/iqazn9p1nwu1wd5odii5.jpeg
---

- Introduction
This post is intended to showcase the use of API Gateway to upload files(images in this example) to AWS Lambda. There are many tutorials about ASP.NET Core application on AWS Lambda but not many regarding the use of uploading files via API Gateway. 
- Set up environment 
Need AWS account for running the application (it uses the rekognition api). 

Using dotnet core templates, create a Web project named CelebrityRekognition.

Add AWS.Rekognition package to identify the images.
<code>    dotnet new web --name CelebrityRekognition</code>
<code>    dotnet add package AWSSDK.rekognition</code>

Once the project is created, add a LamdbaEntryPoint to the project for the AWS Lambda handler. 

As we use the mutlitpart/form-data to transfer the file, we register the content type so that the api gateway treats it as binary data.

<code>
    public class LambdaEntryPoint : APIGatewayProxyFunction
    {
        protected override void Init(IWebHostBuilder builder)
        {
            RegisterResponseContentEncodingForContentType("multipart/form-data", ResponseContentEncoding.Base64);
            builder.UseStartup<Startup>();
        }
    }
</code>

Add the form for uploading the image in the index page. 

<code>
    <form method="post" enctype="multipart/form-data" asp-controller="Home" asp-action="UploadImage" class="form-inline">
        <div class="form-group text-center" align="center">
            <input type="file" name="file" accept=".jpeg, .png, .jpg" required/>
            <input type="submit" value="Upload"/>
        </div>
    </form>
</code>

Add an action to the home controller to handle the image data. 

<code>
	[HttpPost]
	public async Task<IActionResult> UploadImage(IFormFile file)
	{
		AmazonRekognitionClient rekognitionClient = new AmazonRekognitionClient();
		RecognizeCelebritiesRequest recognizeCelebritiesRequest = new RecognizeCelebritiesRequest();

		Image img = new Image();

		var sourceStream = file.OpenReadStream();
		await using (var memoryStream = new MemoryStream())
		{
			await sourceStream.CopyToAsync(memoryStream);
			img.Bytes = memoryStream;
		}
		recognizeCelebritiesRequest.Image = img;
		RecognizeCelebritiesResponse recognizeCelebritiesResponse = await rekognitionClient.RecognizeCelebritiesAsync(recognizeCelebritiesRequest);
		return View("Index", recognizeCelebritiesResponse.CelebrityFaces);
	}
</code>

Run the application locally and test. 

<code>    dotnet run </code>

- cloudformation template
Add the required resources required to run the application in aws lambda. 

<code>

	AWSTemplateFormatVersion: 2010-09-09
	Description: CF Template for createing resources for CelebrityRekognition
	Parameters:
	  S3BUCKETNAME:
		Type: String
		Default: celebrity-rekognition-with-aspnet-core

	Resources:
	  AspNetCoreFunction:
		Type: 'AWS::Lambda::Function'
		Properties:
		  Handler: 'CelebrityRekognition::CelebrityRekognition.LambdaEntryPoint::FunctionHandlerAsync'
		  Runtime: dotnetcore3.1
		  MemorySize: 512
		  Timeout: 10
		  Role:
			Fn::GetAtt:
			- LambdaExecutionRole
			- Arn

	  CelebRekServerlessRestApi:
		Type: "AWS::ApiGateway::RestApi"
		Properties:
		  Name: "celeb-rek-portal-rest-api"
		  Description: "Celebrity rekognition Portal Rest API Gateway"
		  BinaryMediaTypes:
			- 'multipart/form-data'

	  RootMethod:
		Type: 'AWS::ApiGateway::Method'
		Properties:
		  HttpMethod: ANY
		  ResourceId: !GetAtt CelebRekServerlessRestApi.RootResourceId
		  RestApiId: !Ref CelebRekServerlessRestApi
		  AuthorizationType: NONE
		  RequestModels: 
			multipart/form-data : !Ref CelebRekApiGatewayModel
		  Integration:
			Type: AWS_PROXY
			IntegrationHttpMethod: POST
			Uri: !Sub >-
			  arn:aws:apigateway:${AWS::Region}:lambda:path/2015-03-31/functions/${AspNetCoreFunction.Arn}/invocations

	  ProxyResource:
		Type: 'AWS::ApiGateway::Resource'
		Properties:
		  RestApiId: !Ref CelebRekServerlessRestApi
		  ParentId: !GetAtt 
			- CelebRekServerlessRestApi
			- RootResourceId
		  PathPart: '{proxy+}'

	  ProxyMethod:
		Type: 'AWS::ApiGateway::Method'
		Properties:
		  RestApiId: !Ref CelebRekServerlessRestApi
		  ResourceId: !Ref ProxyResource
		  HttpMethod: ANY
		  AuthorizationType: NONE
		  RequestModels: 
			multipart/form-data : !Ref CelebRekApiGatewayModel
		  Integration:
			Type: AWS_PROXY
			IntegrationHttpMethod: POST
			Uri: !Sub >-
			  arn:aws:apigateway:${AWS::Region}:lambda:path/2015-03-31/functions/${AspNetCoreFunction.Arn}/invocations
	 
	  CelebRekServerlessRestApiProdStage:
		Type: 'AWS::ApiGateway::Stage'
		Properties:
		  DeploymentId: !Ref CelebRekServerlessRestApiDeployment
		  RestApiId: !Ref CelebRekServerlessRestApi
		  StageName: Prod

	  CelebRekServerlessRestApiDeployment:
		Type: 'AWS::ApiGateway::Deployment'
		Properties:
		  RestApiId: !Ref CelebRekServerlessRestApi
		  Description: 'RestApi deployment of CelebRek Portal'
		  StageName: Stage

	  CelebRekAspNetCoreFunctionProxyResourcePermissionProd:
		Type: 'AWS::Lambda::Permission'
		Properties:
		  Action: 'lambda:InvokeFunction'
		  Principal: apigateway.amazonaws.com
		  FunctionName: !Ref AspNetCoreFunction
		  SourceArn: !Sub 
			- >-
			  arn:aws:execute-api:${AWS::Region}:${AWS::AccountId}:${__ApiId__}/${__Stage__}/*/*
			- __Stage__: '*'
			  __ApiId__: !Ref CelebRekServerlessRestApi

	  CelebRekAspNetCoreFunctionRootResourcePermissionProd:
		Type: 'AWS::Lambda::Permission'
		Properties:
		  Action: 'lambda:InvokeFunction'
		  Principal: apigateway.amazonaws.com
		  FunctionName: !Ref AspNetCoreFunction
		  SourceArn: !Sub 
			- >-
			  arn:aws:execute-api:${AWS::Region}:${AWS::AccountId}:${__ApiId__}/${__Stage__}/*/
			- __Stage__: '*'
			  __ApiId__: !Ref CelebRekServerlessRestApi

	  CelebRekApiGatewayModel:
		Type: 'AWS::ApiGateway::Model'
		Properties: 
		  ContentType: multipart/form-data
		  Description: Model for form data upload
		  Name: CelebRekMultipartModel
		  RestApiId: !Ref CelebRekServerlessRestApi
		  Schema: {
		   "$schema": "http://json-schema.org/draft-04/schema#",
		   "title": "MediaFileUpload",
		   "type": "object",
		   "properties": {
				"file": { 
					"type": "string" 
					}
				}
		   }

	  LambdaExecutionRole:
		Description: Creating service role in IAM for AWS Lambda  
		Type: AWS::IAM::Role
		Properties:
		  RoleName: 'lambda-execution-role'
		  AssumeRolePolicyDocument:
			Statement:
			- Effect: Allow
			  Principal:
				Service:
				  - lambda.amazonaws.com
				  - apigateway.amazonaws.com            
			  Action: sts:AssumeRole
		  Path: /
		  ManagedPolicyArns:
			- 'arn:aws:iam::aws:policy/AmazonRekognitionFullAccess'
			- 'arn:aws:iam::aws:policy/AmazonS3ReadOnlyAccess'     

	Outputs:
	  CelebRekApiGatewayInvokeURL:
		Value: !Sub "https://${CelebRekServerlessRestApi}.execute-api.${AWS::Region}.amazonaws.com/Prod"

	  CelebRekPortalArn:
		Value: !GetAtt "AspNetCoreFunction.Arn"
</code>

Package the application for deployment. Define the $BUCKETNAME as the one used to store the zip file containing the depolyment. 

<code>    dotnet lambda package-ci --template serverless-template.yml --s3-bucket $BUCKETNAME --output-template template.yml</code>

This will generate template.yml file with the required resources, and uploads the package to $BUCKETNAME

- deploy
Once finished, deploy to AWS via cloudformation.

<code>
	aws cloudformation deploy --template-file template.yml --stack-name celebrity-rekognition-stack --parameter-overrides BucketName=$BUCKETNAME
</code>

Once the deployment finishes, you can go to the stack and get the url for the application. 

- Summary
The code can be found at https://github.com/fasalex/amplify-image-rekognition
