﻿namespace LinqToDB.DataProvider.PostgreSQL
{
	using Extensions;
	using SqlProvider;
	using SqlQuery;

	class PostgreSQLSqlOptimizer : BasicSqlOptimizer
	{
		public PostgreSQLSqlOptimizer(SqlProviderFlags sqlProviderFlags) : base(sqlProviderFlags)
		{
		}

		public override SqlStatement Finalize(SqlStatement statement, bool inlineParameters)
		{
			CheckAliases(statement, int.MaxValue);

			return base.Finalize(statement, inlineParameters);
		}

		public override SqlStatement TransformStatement(SqlStatement statement)
		{
			switch (statement.QueryType)
			{
				case QueryType.Delete : return GetAlternativeDelete((SqlDeleteStatement)statement);
				case QueryType.Update : return GetAlternativeUpdateFrom((SqlUpdateStatement)statement);
				default               : return statement;
			}
		}

		public override ISqlExpression ConvertExpression(ISqlExpression expr)
		{
			expr = base.ConvertExpression(expr);

			if (expr is SqlBinaryExpression)
			{
				var be = (SqlBinaryExpression)expr;

				switch (be.Operation)
				{
					case "^": return new SqlBinaryExpression(be.SystemType, be.Expr1, "#", be.Expr2);
					case "+": return be.SystemType == typeof(string)? new SqlBinaryExpression(be.SystemType, be.Expr1, "||", be.Expr2, be.Precedence): expr;
				}
			}
			else if (expr is SqlFunction)
			{
				var func = (SqlFunction) expr;

				switch (func.Name)
				{
					case "Convert"   :
						if (func.SystemType.ToUnderlying() == typeof(bool))
						{
							var ex = AlternativeConvertToBoolean(func, 1);
							if (ex != null)
								return ex;
						}

						return new SqlExpression(func.SystemType, "Cast({0} as {1})", Precedence.Primary, FloorBeforeConvert(func), func.Parameters[0]);

					case "CharIndex" :
						return func.Parameters.Length == 2?
							new SqlExpression(func.SystemType, "Position({0} in {1})", Precedence.Primary, func.Parameters[0], func.Parameters[1]):
							Add<int>(
								new SqlExpression(func.SystemType, "Position({0} in {1})", Precedence.Primary, func.Parameters[0],
									ConvertExpression(new SqlFunction(typeof(string), "Substring",
										func.Parameters[1],
										func.Parameters[2],
										Sub<int>(ConvertExpression(new SqlFunction(typeof(int), "Length", func.Parameters[1])), func.Parameters[2])))),
								Sub(func.Parameters[2], 1));
				}
			}

			return expr;
		}

	}
}
