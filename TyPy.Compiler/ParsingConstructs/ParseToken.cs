namespace TyPy.Compiler.ParsingConstructs
{
    public enum ParseToken
    {
        Anonymous,
        CompilationUnit,
        Comment,
        FuncType,
        FormatString,
        TypeExpressions,
        Statements,
        Statement,
        StatementNewline,
        SimpleStatement,
        SmallStatement,
        CompoundStatement,
        Assignment,
        AugmentingAssignment,
        GlobalStatement,
        NonLocalStatement,
        YieldStatement,
        AssertStatement,
        DelStatement,
        ImportStatement,
        ImportName,
        ImportFrom,
        ImportFromTargets,
        ImportFromAsNames,
        ImportFromAsName,
        DottedAsNames,
        DottedAsName,
        DottedName,
        IfStatement,
        ElifStatement,
        ElseBlock,
        WhileStatement,
        ForStatement,
        WithStatement,
        WithItem,
        TryStatement,
        ExceptBlock,
        FinallyBlock,
        ReturnStatement,
        RaiseStatement,
        FunctionDefinition,
        FunctionDefinitionRaw,
        Parameters,
        SlashNoDefault,
        SlashWithDefault,
        StarEtc,
        Keywords,
        ParamNoDefault,
        ParamWithDefault,
        ParamMaybeDefault,
        Param,
        Annotation,
        Default,
        Decorators,
        ClassDefinition,
        ClassDefinitionRaw,
        Block,
        StarExpressions,
        StarExpression,
        StarNamedExpressions,
        StarNamedExpression,
        NamedExpression,
        AnnotatedRhs,
        Expressions,
        Expression,
        LambdaDefinition,
        LambdaParameters,
        LambdaSlashNoDefault,
        LambdaSlashWithDefault,
        LambdaParamNoDefault,
        LambdaParamWithDefault,
        LambdaParamMaybeDefault,
        LambdaParam,
        Disjunction,
        Conjunction,
        Inversion,
        Comparison,
        CompareOpBitwiseOrPair,
        EqBitwiseOr,
        NeqBitwiseOr,
        LteBitwiseOr,
        LtBitwiseOr,
        GteBitwiseOr,
        GtBitwiseOr,
        NotInBitwiseOr,
        InBitwiseOr,
        IsNotBitwiseOr,
        IsBitwiseOr,
        BitwiseOr,
        BitwiseXor,
        BitwiseAnd,
        ShiftExpression,
        Sum,
        Term,
        Factor,
        Power,
        Primary,
        Slices,
        Slice,
        Atom,
        Strings,
        List,
        ListComprehension,
        Tuple,
        Group,
        GeneratorExpression,
        Set,
        SetComprehension,
        Dictionary,
        DictionaryComprehension,
        DoubleStarredKeyValuePairs,
        DoubleStarredKeyValuePair,
        KeyValuePair,
        ForIfClauses,
        ForIfClause,
        YieldExpression,
        Arguments,
        Args,
        KwArgs,
        StarredExpression,
        KwArgOrStarred,
        KwArgOrDoubleStarred,
        StarTargets,
        StarTargetsListSeq,
        StarTargetsTupleSeq,
        StarTarget,
        TargetWithStarAtom,
        StarAtom,
        SingleTarget,
        SingleSubscriptAttributeTarget,
        DelTargets,
        DelTarget,
        DelTAtom,
        TPrimary,
        TLookahead,
        FuncTypeComment,
        Params,
        LambdaParams,
        LambdaKwds,
        AwaitPrimary,
        LambdaStarEtc
    }
}