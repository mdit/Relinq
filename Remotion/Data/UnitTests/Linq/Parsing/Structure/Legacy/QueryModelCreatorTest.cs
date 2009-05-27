// This file is part of the re-motion Core Framework (www.re-motion.org)
// Copyright (C) 2005-2009 rubicon informationstechnologie gmbh, www.rubicon.eu
// 
// The re-motion Core Framework is free software; you can redistribute it 
// and/or modify it under the terms of the GNU Lesser General Public License 
// version 3.0 as published by the Free Software Foundation.
// 
// re-motion is distributed in the hope that it will be useful, 
// but WITHOUT ANY WARRANTY; without even the implied warranty of 
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the 
// GNU Lesser General Public License for more details.
// 
// You should have received a copy of the GNU Lesser General Public License
// along with re-motion; if not, see http://www.gnu.org/licenses.
// 
using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using NUnit.Framework;
using Remotion.Collections;
using Remotion.Data.Linq;
using Remotion.Data.Linq.Clauses;
using Remotion.Data.Linq.Expressions;
using Remotion.Data.Linq.Parsing;
using Remotion.Data.Linq.Parsing.Structure.Legacy;
using Remotion.Data.UnitTests.Linq.TestQueryGenerators;

namespace Remotion.Data.UnitTests.Linq.Parsing.Structure.Legacy
{
  [TestFixture]
  public class QueryModelCreatorTest
  {
    private IQueryable<Student> _source;
    private Expression _root;
    private ParseResultCollector _result;
    private QueryModelCreator _modelCreator;
    private FromExpressionData _firstFromExpressionData;

    [SetUp]
    public void SetUp ()
    {
      _source = ExpressionHelper.CreateQuerySource();
      _root = ExpressionHelper.CreateExpression();
      _result = new ParseResultCollector (_root);
      _firstFromExpressionData = new FromExpressionData (Expression.Constant (_source), ExpressionHelper.CreateParameterExpression());
      _result.AddBodyExpression (_firstFromExpressionData);
      _modelCreator = new QueryModelCreator (_root, _result);
    }

    [Test]
    [ExpectedException (typeof (ParserException), ExpectedMessage = "There is no projection for the select clause.")]
    public void NoProjectionForSelectClause ()
    {
      _modelCreator.CreateQueryModel ();
    }

    [Test]
    public void ResultType_Simple ()
    {
      _result.AddProjectionExpression (ExpressionHelper.CreateLambdaExpression ());
      QueryModel model = _modelCreator.CreateQueryModel ();
      Assert.AreEqual (_root.Type, model.ResultType);
    }

    [Test]
    public void ResultType_WithProjection ()
    {
      IQueryable<Tuple<Student, string, string, string>> query =
          SelectTestQueryGenerator.CreateSimpleQueryWithSpecialProjection (ExpressionHelper.CreateQuerySource());
      var modelCreator = new QueryModelCreator (query.Expression, _result);

      _result.AddProjectionExpression (ExpressionHelper.CreateLambdaExpression ());
      QueryModel model = modelCreator.CreateQueryModel ();
      Assert.AreEqual (typeof (IQueryable<Tuple<Student, string, string, string>>), model.ResultType);
    }

    [Test]
    public void FirstBodyClause_TranslatedIntoMainFromClause ()
    {
      var additionalFromExpression = new FromExpressionData (ExpressionHelper.CreateLambdaExpression(), Expression.Parameter(typeof(Student),"p"));
      _result.AddBodyExpression (additionalFromExpression);
      _result.AddProjectionExpression (ExpressionHelper.CreateLambdaExpression ());

      QueryModel model = _modelCreator.CreateQueryModel ();
      Assert.IsNotNull (model.MainFromClause);
      Assert.AreSame (_firstFromExpressionData.Identifier, model.MainFromClause.Identifier);
      Assert.AreSame (_firstFromExpressionData.TypedExpression, model.MainFromClause.QuerySource);
      Assert.AreNotSame (additionalFromExpression.Identifier, model.MainFromClause.Identifier);
    }

    [Test]
    public void LastProjectionExpresion_TranslatedIntoSelectClause_NoFromClauses ()
    {
      _result.AddProjectionExpression (ExpressionHelper.CreateLambdaExpression());
      
      QueryModel model = _modelCreator.CreateQueryModel();
      Assert.AreEqual (0, model.BodyClauses.Count);

      var selectClause = model.SelectOrGroupClause as SelectClause;
      Assert.IsNotNull (selectClause);
      Assert.AreSame (_result.ProjectionExpressions[0], selectClause.ProjectionExpression);
    }
    
    [Test]
    [ExpectedException (typeof (ParserException), ExpectedMessage = "From expression 'i' (() => 0) doesn't have a projection expression.")]
    public void FromExpression_WithoutProjection ()
    {
      var additionalFromExpression = new FromExpressionData (ExpressionHelper.CreateLambdaExpression (), ExpressionHelper.CreateParameterExpression ());
      _result.AddBodyExpression (additionalFromExpression);

      _modelCreator.CreateQueryModel ();
    }

    [Test]
    public void BodyExpressions_TranslatedIntoAdditionalFromClauses ()
    {
      _result.AddProjectionExpression (ExpressionHelper.CreateLambdaExpression());
      _result.AddProjectionExpression (ExpressionHelper.CreateLambdaExpression());

      var fromExpression1 = new FromExpressionData (ExpressionHelper.CreateLambdaExpression (), Expression.Parameter (typeof (int), "p"));
      var fromExpression2 = new FromExpressionData (ExpressionHelper.CreateLambdaExpression (), Expression.Parameter (typeof (int), "j"));

      _result.AddBodyExpression (fromExpression1);
      _result.AddBodyExpression (fromExpression2);

      QueryModel model = _modelCreator.CreateQueryModel ();     
      
      Assert.AreEqual (2, model.BodyClauses.Count);

      var additionalFromClause1 = model.BodyClauses.First() as AdditionalFromClause;
      Assert.IsNotNull (additionalFromClause1);
      Assert.AreSame (fromExpression1.TypedExpression, additionalFromClause1.FromExpression);
      Assert.AreSame (fromExpression1.Identifier, additionalFromClause1.Identifier);
      Assert.AreSame (_result.ProjectionExpressions[0], additionalFromClause1.ProjectionExpression);

      var additionalFromClause2 = model.BodyClauses.Last () as AdditionalFromClause;
      Assert.IsNotNull (additionalFromClause2);
      Assert.AreSame (fromExpression2.TypedExpression, additionalFromClause2.FromExpression);
      Assert.AreSame (fromExpression2.Identifier, additionalFromClause2.Identifier);
      Assert.AreSame (_result.ProjectionExpressions[1], additionalFromClause2.ProjectionExpression);
    }

    [Test]
    public void BodyExpressions_TranslatedIntoSubQueryFromClauses ()
    {
      _result.AddProjectionExpression (ExpressionHelper.CreateLambdaExpression());

      IQueryable<Student> subQuery = SelectTestQueryGenerator.CreateSimpleQuery (ExpressionHelper.CreateQuerySource());
      QueryModel subQueryModel = ExpressionHelper.ParseQuery (subQuery);
      var subQueryExpression = new SubQueryExpression (subQueryModel);
      var fromExpression1 = new FromExpressionData (Expression.Lambda (subQueryExpression, Expression.Parameter (typeof (int), "p")),
          Expression.Parameter (typeof (int), "p"));

      _result.AddBodyExpression (fromExpression1);

      QueryModel model = _modelCreator.CreateQueryModel();

      Assert.AreEqual (1, model.BodyClauses.Count);

      var subQueryFromClause1 = model.BodyClauses[0] as SubQueryFromClause;
      Assert.IsNotNull (subQueryFromClause1);
      Assert.AreSame (subQueryModel, subQueryFromClause1.SubQueryModel);
      Assert.AreSame (fromExpression1.Identifier, subQueryFromClause1.Identifier);
      Assert.AreSame (_result.ProjectionExpressions[0], subQueryFromClause1.ProjectionExpression);
    }

    [Test]
    public void BodyExpressionsWithMemberExpression_TranslatedIntoMemberFromClause ()
    {
      _result.AddProjectionExpression (ExpressionHelper.CreateLambdaExpression ());

      var bodyExpression = Expression.MakeMemberAccess (Expression.Constant ("test"), typeof (string).GetProperty ("Length"));
      var fromExpression = Expression.Lambda (bodyExpression);
      var fromExpression2 = new FromExpressionData (fromExpression, Expression.Parameter (typeof (int), "j"));

      _result.AddBodyExpression (fromExpression2);

      QueryModel model = _modelCreator.CreateQueryModel ();

      Assert.AreEqual (1, model.BodyClauses.Count);

      var memberFromClause = model.BodyClauses.Last () as MemberFromClause;
      Assert.IsNotNull (memberFromClause);
      Assert.AreSame (fromExpression2.TypedExpression, memberFromClause.FromExpression);
      Assert.AreSame (fromExpression2.Identifier, memberFromClause.Identifier);
      Assert.AreSame (_result.ProjectionExpressions[0], memberFromClause.ProjectionExpression);
    }

    [Test]
    public void LastProjectionExpresion_TranslatedIntoSelectClause_WithFromClauses ()
    {
      _result.AddProjectionExpression (ExpressionHelper.CreateLambdaExpression());
      _result.AddProjectionExpression (ExpressionHelper.CreateLambdaExpression());

      var fromExpressionData = new FromExpressionData (ExpressionHelper.CreateLambdaExpression (), Expression.Parameter (typeof (int), "p"));

      _result.AddBodyExpression (fromExpressionData);

      QueryModel model = _modelCreator.CreateQueryModel ();
      
      Assert.AreEqual (1, model.BodyClauses.Count);

      var selectClause = model.SelectOrGroupClause as SelectClause;
      Assert.IsNotNull (selectClause);
      Assert.AreSame (_result.ProjectionExpressions[1], selectClause.ProjectionExpression);
    }

    [Test]
    public void BodyExpression_TranslatedIntoWhereClause ()
    {
      _result.AddProjectionExpression (ExpressionHelper.CreateLambdaExpression());

      var whereExpressionData = new WhereExpressionData (ExpressionHelper.CreateLambdaExpression());

      _result.AddBodyExpression (whereExpressionData);

      QueryModel model = _modelCreator.CreateQueryModel ();

      Assert.AreEqual (1, model.BodyClauses.Count);

      var whereClause = model.BodyClauses.First() as WhereClause;
      Assert.IsNotNull (whereClause);
      Assert.AreSame (whereExpressionData.TypedExpression, whereClause.BoolExpression);
    }

    [Test]
    public void BodyExpression__TranslatedIntoLetClause ()
    {
      _result.AddProjectionExpression (ExpressionHelper.CreateLambdaExpression ());

      ParameterExpression identifier = Expression.Parameter (typeof (string), "x");
      var letExpressionData = new LetExpressionData (identifier,ExpressionHelper.CreateLambdaExpression());

      _result.AddBodyExpression (letExpressionData);

      QueryModel model = _modelCreator.CreateQueryModel ();

      Assert.AreEqual (1, model.BodyClauses.Count);

      var letClause = model.BodyClauses.First () as LetClause;
      Assert.IsNotNull (letClause);
      Assert.AreSame (letExpressionData.TypedExpression, letClause.Expression);
    }

    [Test]
    public void BodyExpression_TranslatedIntoOrderByClause ()
    {
      _result.AddProjectionExpression (ExpressionHelper.CreateLambdaExpression());

      var orderExpressionData = new OrderExpressionData (true, OrderingDirection.Asc, ExpressionHelper.CreateLambdaExpression());

      _result.AddBodyExpression (orderExpressionData);

      QueryModel model = _modelCreator.CreateQueryModel ();
      
      Assert.AreEqual (1, model.BodyClauses.Count);

      var orderByClause = model.BodyClauses.First() as OrderByClause;
      Assert.IsNotNull (orderByClause);
      Assert.AreEqual (1, orderByClause.OrderingList.Count);
      Assert.AreSame (orderExpressionData.TypedExpression, orderByClause.OrderingList.First().Expression);
      Assert.AreEqual (orderExpressionData.OrderingDirection, orderByClause.OrderingList.First().OrderingDirection);
      Assert.AreSame (model.MainFromClause, orderByClause.PreviousClause);
      Assert.AreSame (orderByClause, orderByClause.OrderingList.First ().OrderByClause);
    }

    [Test]
    public void OrderByThenBy_TranslatedIntoOrderByClause ()
    {
      _result.AddProjectionExpression (ExpressionHelper.CreateLambdaExpression());

      var orderExpression1 = new OrderExpressionData (true, OrderingDirection.Asc, ExpressionHelper.CreateLambdaExpression());
      var orderExpression2 = new OrderExpressionData (false, OrderingDirection.Desc, ExpressionHelper.CreateLambdaExpression());
      var orderExpression3 = new OrderExpressionData (true, OrderingDirection.Asc, ExpressionHelper.CreateLambdaExpression());

      _result.AddBodyExpression (orderExpression1);
      _result.AddBodyExpression (orderExpression2);
      _result.AddBodyExpression (orderExpression3);

      QueryModel model = _modelCreator.CreateQueryModel();
      
      
      Assert.AreEqual (2, model.BodyClauses.Count);

      var orderByClause1 = model.BodyClauses.First() as OrderByClause;
      var orderByClause2 = model.BodyClauses.Last() as OrderByClause;

      Assert.IsNotNull (orderByClause1);
      Assert.IsNotNull (orderByClause2);

      Assert.AreEqual (2, orderByClause1.OrderingList.Count);
      Assert.AreEqual (1, orderByClause2.OrderingList.Count);

      Assert.AreSame (orderExpression1.TypedExpression, orderByClause1.OrderingList.First().Expression);
      Assert.AreEqual (orderExpression1.OrderingDirection, orderByClause1.OrderingList.First().OrderingDirection);
      Assert.AreSame (model.MainFromClause, orderByClause1.PreviousClause);
      Assert.AreSame (orderByClause1, orderByClause1.OrderingList.First ().OrderByClause);
      Assert.AreSame (orderExpression2.TypedExpression, orderByClause1.OrderingList.Last().Expression);
      Assert.AreEqual (orderExpression2.OrderingDirection, orderByClause1.OrderingList.Last().OrderingDirection);
      Assert.AreSame (orderByClause1, orderByClause2.PreviousClause);
      Assert.AreSame (orderByClause1, orderByClause1.OrderingList.Last ().OrderByClause);
      Assert.AreSame (orderExpression3.TypedExpression, orderByClause2.OrderingList.First().Expression);
      Assert.AreEqual (orderExpression3.OrderingDirection, orderByClause2.OrderingList.First().OrderingDirection);
      Assert.AreSame (orderByClause2, orderByClause2.OrderingList.First().OrderByClause);
    }

    [Test]
    public void MultiExpression_IntegrationTest ()
    {
      _result.AddProjectionExpression (ExpressionHelper.CreateLambdaExpression());
      _result.AddProjectionExpression (ExpressionHelper.CreateLambdaExpression());
      _result.AddProjectionExpression (ExpressionHelper.CreateLambdaExpression());

      var fromExpression1 = new FromExpressionData (ExpressionHelper.CreateLambdaExpression(), Expression.Parameter (typeof (Student), "s1"));
      var fromExpression2 = new FromExpressionData (ExpressionHelper.CreateLambdaExpression(), Expression.Parameter (typeof (Student), "s2"));
      var whereExpression1 = new WhereExpressionData (ExpressionHelper.CreateLambdaExpression());
      var whereExpression2 = new WhereExpressionData (ExpressionHelper.CreateLambdaExpression());

      var orderExpression1 = new OrderExpressionData (true, OrderingDirection.Asc, ExpressionHelper.CreateLambdaExpression());
      var orderExpression2 = new OrderExpressionData (false, OrderingDirection.Desc, ExpressionHelper.CreateLambdaExpression());
      var orderExpression3 = new OrderExpressionData (true, OrderingDirection.Asc, ExpressionHelper.CreateLambdaExpression());

      _result.AddBodyExpression (fromExpression1);
      _result.AddBodyExpression (fromExpression2);
      _result.AddBodyExpression (whereExpression1);
      _result.AddBodyExpression (whereExpression2);
      _result.AddBodyExpression (orderExpression1);
      _result.AddBodyExpression (orderExpression2);
      _result.AddBodyExpression (orderExpression3);


      QueryModel model = _modelCreator.CreateQueryModel ();

      var orderByClause1 = model.BodyClauses.Skip (4).First() as OrderByClause;
      var orderByClause2 = model.BodyClauses.Skip (5).First () as OrderByClause;

      var fromClause1 = model.BodyClauses.First () as AdditionalFromClause;
      Assert.IsNotNull (fromClause1);
      Assert.AreSame (fromExpression1.Identifier, fromClause1.Identifier);
      Assert.AreSame (fromExpression1.TypedExpression, fromClause1.FromExpression);
      Assert.AreSame (_result.ProjectionExpressions[0], fromClause1.ProjectionExpression);
      Assert.AreSame (model.MainFromClause, fromClause1.PreviousClause);

      var fromClause2 = model.BodyClauses.Skip (1).First () as AdditionalFromClause;
      Assert.IsNotNull (fromClause2);
      Assert.AreSame (fromExpression2.Identifier, fromClause2.Identifier);
      Assert.AreSame (fromExpression2.TypedExpression, fromClause2.FromExpression);
      Assert.AreSame (_result.ProjectionExpressions[1], fromClause2.ProjectionExpression);
      Assert.AreSame (fromClause1, fromClause2.PreviousClause);

      var whereClause1 = model.BodyClauses.Skip (2).First () as WhereClause;
      Assert.IsNotNull (whereClause1);
      Assert.AreSame (whereExpression1.TypedExpression, whereClause1.BoolExpression);
      Assert.AreSame (fromClause2, whereClause1.PreviousClause);

      var whereClause2 = model.BodyClauses.Skip (3).First () as WhereClause;
      Assert.IsNotNull (whereClause2);
      Assert.AreSame (whereExpression2.TypedExpression, whereClause2.BoolExpression);
      Assert.AreSame (whereClause1, whereClause2.PreviousClause);

      Assert.IsNotNull (orderByClause1);
      Assert.AreSame (orderExpression1.TypedExpression, orderByClause1.OrderingList.First().Expression);
      Assert.AreSame (whereClause2, orderByClause1.PreviousClause);
      Assert.AreSame (orderByClause1, orderByClause1.OrderingList.First ().OrderByClause);
      Assert.AreSame (whereClause2, orderByClause1.PreviousClause);

      Assert.AreSame (orderExpression2.TypedExpression, orderByClause1.OrderingList.Last().Expression);
      Assert.AreSame (orderByClause1, orderByClause1.OrderingList.Last().OrderByClause);
      
      Assert.IsNotNull (orderByClause2);
      Assert.AreSame (orderExpression3.TypedExpression, orderByClause2.OrderingList.First().Expression);
      Assert.AreSame (orderByClause2, orderByClause2.OrderingList.First ().OrderByClause);
      Assert.AreSame (orderByClause1, orderByClause2.PreviousClause);

      var selectClause = model.SelectOrGroupClause as SelectClause;
      Assert.IsNotNull (selectClause);
      Assert.AreSame (_result.ProjectionExpressions[2], selectClause.ProjectionExpression);
      Assert.AreSame (orderByClause2, selectClause.PreviousClause);
    }

  }
}