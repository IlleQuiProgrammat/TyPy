grammar PEG;

compilation_unit: (statement? COMMENT? '\n')*;
statement: NAME ':' ('\n' '|')? rule ('\n' '|' rule)*;
rule: alternative COMMENT?;
alternative: sub_primary+ ('|' sub_primary+)*;
optional: '[' rule ']' | primary '?';
many_or_none: primary '*';
many: primary '+';
separated_expr: primary '.' primary '+';
positive_lookahead: '&' primary;
negative_lookahead: '!' primary;
sub_primary: optional | many_or_none | many | separated_expr | positive_lookahead | negative_lookahead | primary;
primary: '(' rule ')' | CUT | NAME | STRING;


WHITESPACE: (' ' | '\t') -> skip;
COMMENT: '#' (~'\n')*;
NAME: [a-zA-Z_]+;
// probably a better way of doing this
STRING: '"' (~('"' | '\n')|'\\"')* '"' | '\'' (~('\'' | '\n')|'\\\'')* '\'';
CUT: '~';
