using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NTratch
{
	public class ExceptionFlow
	{
		public const string DOC_SEMANTIC = "doc_semantic";
		public const string DOC_SYNTAX = "doc_syntax";
		public const string THROW = "throw";
	
		//Thrown Exception Type info
		private string thrownTypeName;
		private INamedTypeSymbol thrownType;

		//Original method info
		//Assumption: all declared methods are successfully binded.
		//Store where this exception was found. 
		///TODO: If found by different ways, the value will be based on precedence, according to this order:
		//			Throw -> deepest level found -> others...
		private string originalMethodBindingKey = "";

		private int LevelFound = 0;

		private bool isDocSemantic = false;
		private bool isDocSyntax = false;
		private bool isThrow = false;

		public ExceptionFlow(INamedTypeSymbol thrownType, string originType, string originalMethodBindingKey, int levelFound)
		{
			setThrownType(thrownType);
			setOriginFlag(originType);
			setOriginalMethodBindingKey(originalMethodBindingKey);
			setLevelFound(levelFound);
		}

		public ExceptionFlow(string exceptionName, string originType, string originalMethodBindingKey, int levelFound)
		{
			setThrownTypeName(exceptionName);
			setOriginFlag(originType);
			setOriginalMethodBindingKey(originalMethodBindingKey);
			setLevelFound(levelFound);
		}

		public ExceptionFlow(ExceptionFlow exceptionFlow)
		{
			if(exceptionFlow.getThrownType() != null)
				setThrownType(exceptionFlow.getThrownType());
			setThrownTypeName(exceptionFlow.getThrownTypeName());
			setOriginalMethodBindingKey(exceptionFlow.getOriginalMethodBindingKey());
			setLevelFound(exceptionFlow.getLevelFound());

			setIsDocSemantic(exceptionFlow.getIsDocSemantic());
			setIsDocSyntax(exceptionFlow.getIsDocSyntax());
			setIsThrow(exceptionFlow.getIsThrow());
		}

		public string getThrownTypeName()
		{
			return thrownTypeName;
		}

		public void setThrownTypeName(string thrownTypeName)
		{
			this.thrownTypeName = thrownTypeName;
		}

		public INamedTypeSymbol getThrownType()
		{
			return thrownType;
		}

		public void setThrownType(INamedTypeSymbol thrownType)
		{
			this.thrownType = thrownType;
			setThrownTypeName(thrownType.ToString());
		}

		public int getLevelFound()
		{
			return LevelFound;
		}

		public void setLevelFound(int levelFound)
		{
			LevelFound = levelFound;
		}

		
		public bool getIsDocSemantic()
		{
			return isDocSemantic;
		}

		public void setIsDocSemantic(bool isDocSemantic)
		{
			this.isDocSemantic = isDocSemantic;
		}

		public bool getIsDocSyntax()
		{
			return isDocSyntax;
		}

		public void setIsDocSyntax(bool isDocSyntax)
		{
			this.isDocSyntax = isDocSyntax;
		}

		public bool getIsThrow()
		{
			return isThrow;
		}

		public void setIsThrow(bool isThrow)
		{
			this.isThrow = isThrow;
		}

		public string getOriginalMethodBindingKey()
		{
			return originalMethodBindingKey;
		}

		public void setOriginalMethodBindingKey(string originalMethodBindingKey)
		{
			this.originalMethodBindingKey = originalMethodBindingKey;
		}

		public void setOriginFlag(string originType)
		{
			switch (originType)
			{
				case DOC_SEMANTIC:
					setIsDocSemantic(true);
					break;
				case DOC_SYNTAX:
					setIsDocSyntax(true);
					break;
				case THROW:
					setIsThrow(true);
					break;
				default: break;
			}
		}

	override public string ToString()
		{
			StringBuilder sb = new StringBuilder();

			sb.Append(getThrownTypeName());

			return sb.ToString();
		}
	}
}
